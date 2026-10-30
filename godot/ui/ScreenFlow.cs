using System.Collections.Immutable;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Content.Check;
using AncientOrder.Rules.Saves;
using AncientOrder.Rules.Session;
using AncientOrder.Rules.State;
using AncientOrder.Stage;
using Godot;

namespace AncientOrder.Ui;

/// <summary>
/// The root of the running game and the only node that holds the GameSession. It moves the player between screens and renders every snapshot:
/// one base screen at a time (start, town, hand-off, or the content error screen) and overlays on top (conversation panel, game menu, party or save screen).
/// It routes only; presenters and screens do the work and reach the session through the flow. Layers, in order: TownStage (3D), ScreenLayer, OverlayLayer, TopLayer
/// (error banner and the stand-in marker).
/// Screen slots (T16 to T22): each screen is a scene at the path named by the *Path constants below; when the scene does not exist yet a plain placeholder stands in.
/// A scene's root script may implement IFlowScreen to get the flow. The instances are exposed as StartScreen, HandOffScreen, ConversationPanel, PartyScreen and SaveScreen,
/// and presenters attach in InstallPresenters(). Every snapshot goes out through SnapshotRendered after the screens are matched to it.
/// </summary>
public partial class ScreenFlow : Node
{
    public const string StartScreenPath = "res://ui/screens/StartScreen.tscn";
    public const string HandOffScreenPath = "res://ui/screens/HandOffScreen.tscn";
    public const string ContentErrorScreenPath = "res://ui/screens/ContentErrorScreen.tscn";
    public const string ConversationPanelPath = "res://ui/conversation/ConversationPanel.tscn";
    public const string GameMenuPath = "res://ui/menu/GameMenu.tscn";
    public const string PartyScreenPath = "res://ui/party/PartyScreen.tscn";
    public const string SaveScreenPath = "res://ui/saves/SaveScreen.tscn";

    private enum Overlay
    {
        None,
        Party,
        Saves,
    }

    private enum BaseScreen
    {
        Start,
        Town,
        HandOff,
    }

    private TownStage _stage = null!;
    private Control _screenHost = null!;
    private Control _overlayHost = null!;
    private Ui.ErrorBanner _banner = null!;
    private Label _standInMarker = null!;
    private IDisposable? _subscription;
    private GameMenu? _menu;
    private ContentErrorScreen? _contentError;
    private bool _atStart = true;
    private bool _menuOpen;
    private Overlay _overlay = Overlay.None;
    private BaseScreen _base = BaseScreen.Start;
    private bool _started;
    private bool _exited;
    private bool _conversationHold;

    /// <summary>When true (the default) _Ready takes the content and the session from a new GameHost. Tests set it false and call Start themselves.</summary>
    [Export]
    public bool AutoStart { get; set; } = true;

    public GameContent? Content { get; private set; }

    public GameSession? Session { get; private set; }

    public Control? StartScreen { get; private set; }

    public Control? HandOffScreen { get; private set; }

    public Control? ConversationPanel { get; private set; }

    public Control? PartyScreen { get; private set; }

    public Control? SaveScreen { get; private set; }

    /// <summary>The save presenter (set when the flow starts with a game); screens use it for RequestSlots.</summary>
    public SavePresenter? Saves { get; private set; }

    public TownStage Stage => _stage;

    /// <summary>
    /// Set by the conversation presenter while the last choice's outcome waits for Close: the flow then treats the conversation as still on screen
    /// (panel visible, town paused) through every render, menu round-trips included. Setting it renders again. A load drops it (see ConversationHoldDropped).
    /// </summary>
    public bool ConversationHold
    {
        get => _conversationHold;
        set
        {
            if (_conversationHold == value)
            {
                return;
            }

            _conversationHold = value;
            Refresh();
        }
    }

    /// <summary>Raised when a load drops the conversation hold, so the presenter can show the loaded game's own step.</summary>
    public event Action? ConversationHoldDropped;

    public SaveScreenMode SaveMode { get; private set; } = SaveScreenMode.LoadOnly;

    public bool IsMenuOpen => _menuOpen;

    public bool IsPartyOpen => _overlay == Overlay.Party;

    public bool IsSavesOpen => _overlay == Overlay.Saves;

    public bool IsAtStart => _base == BaseScreen.Start;

    public bool IsAtHandOff => _base == BaseScreen.HandOff;

    public bool IsAtTown => _base == BaseScreen.Town;

    public bool IsContentErrorShown => _contentError is { Visible: true };

    /// <summary>Every snapshot, in order, after the screens have been matched to it. Read only; for presenters and the dev tools.</summary>
    public event Action<SessionSnapshot>? SnapshotRendered;

    public event Action<LoadReport>? LoadCompleted;

    public event Action<ContentCheckReport>? ContentChecked;

    /// <summary>Raised instead of quitting the game when anything subscribes to it (tests intercept the quit here).</summary>
    public event Action? QuitRequested;

    /// <summary>Raised when the party screen opens, so its presenter can refresh.</summary>
    public event Action? PartyOpened;

    /// <summary>Raised when the save and load screen opens, so its presenter can list the slots.</summary>
    public event Action<SaveScreenMode>? SavesOpened;

    public override void _Ready()
    {
        _stage = GetNode<TownStage>("TownStage");
        _screenHost = GetNode<Control>("ScreenLayer/ScreenHost");
        _overlayHost = GetNode<Control>("OverlayLayer/OverlayHost");
        _banner = GetNode<Ui.ErrorBanner>("TopLayer/ErrorBanner");
        _standInMarker = GetNode<Label>("TopLayer/StandInMarker");
        _standInMarker.Visible = false;
        if (AutoStart)
        {
            var host = new GameHost();
            Start(host.Content, host.Session, host.Report);
        }
    }

    public override void _ExitTree()
    {
        _exited = true;
        _subscription?.Dispose();
        _subscription = null;
    }

    /// <summary>Starts the flow with the given content and session (the tests' seam). Without content or a session it shows the content error screen with the report's problems.</summary>
    public void Start(GameContent? content, GameSession? session, ContentCheckReport? report)
    {
        if (_started)
        {
            throw new InvalidOperationException("ScreenFlow already started.");
        }

        _started = true;
#if AO_DEVTOOLS
        if (OS.IsDebugBuild())
        {
            AncientOrder.DevTools.DevTools.Install(this);
        }
#endif
        if (content is null || session is null)
        {
            ShowContentError(report?.Problems ?? ImmutableArray<RuleErrorDetail>.Empty);
            if (report is not null)
            {
                ContentChecked?.Invoke(report);
            }

            return;
        }

        Content = content;
        Session = session;
        _standInMarker.Visible = content.Pack.StandIn;
        _stage.Init(session, ShowError);
        BuildScreens();
        InstallPresenters();
        _subscription = session.Subscribe(snapshot => Callable.From(() =>
        {
            if (!_exited && GodotObject.IsInstanceValid(this))
            {
                Deliver(snapshot);
            }
        }).CallDeferred());
        ContentChecked?.Invoke(report ?? new ContentCheckReport(content, ImmutableArray<RuleErrorDetail>.Empty, ImmutableArray<RuleErrorDetail>.Empty));
        Render(session.Snapshot, raise: false);
    }

    /// <summary>Slot for T16 (conversation presenter) and T18 (party and save presenters): create each presenter here, handing it this flow and its screen.</summary>
    private void InstallPresenters()
    {
        if (ConversationPanel is IConversationPanel conversationPanel)
        {
            _ = new ConversationPresenter(this, conversationPanel);
        }

        _ = new PartyPresenter(this, PartyScreen as IPartyScreen);
        Saves = new SavePresenter(this, SaveScreen as ISaveScreen);
    }

    /// <summary>Begins a new game from the content's start and leaves the start screen.</summary>
    public void StartNewGame()
    {
        if (Session is null)
        {
            return;
        }

        var result = Session.NewGame();
        if (!result.IsOk)
        {
            ShowError(result.Error);
            return;
        }

        _atStart = false;
        CloseAll();
        DropConversationHold();
        Refresh();
    }

    /// <summary>Goes back to the start screen (the session keeps its game until a new game or a load replaces it).</summary>
    public void ReturnToStart()
    {
        if (Session is null)
        {
            return;
        }

        _atStart = true;
        CloseAll();
        DropConversationHold();
        Refresh();
    }

    /// <summary>Ends the game. With a QuitRequested subscriber (the tests) it only raises the event; otherwise it quits the tree.</summary>
    public void Quit()
    {
        if (QuitRequested is { } handler)
        {
            handler();
            return;
        }

        GetTree().Quit();
    }

    private void DropConversationHold()
    {
        if (_conversationHold)
        {
            _conversationHold = false;
            ConversationHoldDropped?.Invoke();
        }
    }

    public void OpenParty()
    {
        if (Session is null || PartyScreen is null)
        {
            return;
        }

        SetOverlay(Overlay.Party);
        PartyOpened?.Invoke();
    }

    public void OpenSaves(SaveScreenMode mode)
    {
        if (Session is null || SaveScreen is null)
        {
            return;
        }

        SaveMode = mode;
        SetOverlay(Overlay.Saves);
        SavesOpened?.Invoke(mode);
    }

    /// <summary>Closes the party or save screen (back to the menu, or to the start screen) and then, on a second call, the menu.</summary>
    public void Back()
    {
        if (_overlay != Overlay.None)
        {
            SetOverlay(Overlay.None);
        }
        else if (_menuOpen)
        {
            CloseMenu();
        }
    }

    public void OpenMenu()
    {
        if (_menu is null || _menuOpen || _base == BaseScreen.Start)
        {
            return;
        }

        _menuOpen = true;
        _menu.Visible = true;
        Refresh();
        FocusIn(_menu);
    }

    public void CloseMenu()
    {
        if (_menu is null || !_menuOpen)
        {
            return;
        }

        _overlay = Overlay.None;
        PartyScreen!.Visible = false;
        SaveScreen!.Visible = false;
        _menuOpen = false;
        _menu.Visible = false;
        Refresh();
    }

    /// <summary>Called by the save presenter after a load succeeded: leaves the start screen, closes the menu and screens, and shows where the save left off.</summary>
    public void NotifyLoaded(LoadReport report) => OnMainThread(() =>
    {
        _atStart = false;
        CloseAll();
        DropConversationHold();
        Refresh();
        LoadCompleted?.Invoke(report);
    });

    /// <summary>Shows a refusal in the error banner. Safe to call from any thread.</summary>
    public void ShowError(RuleError error) => OnMainThread(() => _banner.Show(error));

    /// <summary>Shows a plain message in the error banner. Safe to call from any thread.</summary>
    public void ShowMessage(string message) => OnMainThread(() => _banner.Show(message));

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Session is null || !@event.IsActionPressed("ui_menu") || @event.IsEcho())
        {
            return;
        }

        if (_overlay != Overlay.None || _menuOpen)
        {
            Back();
        }
        else
        {
            OpenMenu();
        }

        GetViewport().SetInputAsHandled();
    }

    private void Deliver(SessionSnapshot snapshot) => Render(snapshot, raise: true);

    private void Refresh()
    {
        if (Session is not null)
        {
            Render(Session.Snapshot, raise: false);
        }
    }

    private void Render(SessionSnapshot snapshot, bool raise)
    {
        _base = _atStart ? BaseScreen.Start
            : snapshot.HandOff is not null ? BaseScreen.HandOff
            : snapshot.Town is not null ? BaseScreen.Town
            : BaseScreen.Start;

        if (snapshot.Town is not null)
        {
            _stage.Apply(snapshot.Town);
        }

        var covered = _menuOpen || _overlay != Overlay.None;
        _stage.Visible = _base == BaseScreen.Town;
        StartScreen!.Visible = _base == BaseScreen.Start;
        HandOffScreen!.Visible = _base == BaseScreen.HandOff;
        var inConversation = snapshot.Conversation is not null || _conversationHold;
        ConversationPanel!.Visible = inConversation && _base != BaseScreen.Start;
        ConversationPanel.ProcessMode = covered ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;
        if (_stage.Visible && !covered && !_conversationHold)
        {
            _stage.Resume();
        }
        else
        {
            _stage.Pause();
        }

        if (raise)
        {
            SnapshotRendered?.Invoke(snapshot);
        }

        if (_base != BaseScreen.Town && !covered)
        {
            FocusIn(_base == BaseScreen.Start ? StartScreen : HandOffScreen);
        }
    }

    private void SetOverlay(Overlay overlay)
    {
        _overlay = overlay;
        PartyScreen!.Visible = overlay == Overlay.Party;
        SaveScreen!.Visible = overlay == Overlay.Saves;
        _menu!.Visible = _menuOpen && overlay == Overlay.None;
        Refresh();
        if (overlay == Overlay.Party)
        {
            FocusIn(PartyScreen);
        }
        else if (overlay == Overlay.Saves)
        {
            FocusIn(SaveScreen);
        }
        else if (_menuOpen)
        {
            FocusIn(_menu);
        }
    }

    private void CloseAll()
    {
        _overlay = Overlay.None;
        _menuOpen = false;
        PartyScreen!.Visible = false;
        SaveScreen!.Visible = false;
        _menu!.Visible = false;
    }

    private void BuildScreens()
    {
        StartScreen = AddScreen(_screenHost, StartScreenPath, FallbackStart);
        HandOffScreen = AddScreen(_screenHost, HandOffScreenPath, FallbackHandOff);
        ConversationPanel = AddScreen(_overlayHost, ConversationPanelPath, () => FallbackPanel("(conversation panel not built yet)", withClose: false));
        _menu = (GameMenu)AddScreen(_overlayHost, GameMenuPath, null);
        PartyScreen = AddScreen(_overlayHost, PartyScreenPath, () => FallbackPanel("(party screen not built yet)", withClose: true));
        SaveScreen = AddScreen(_overlayHost, SaveScreenPath, () => FallbackPanel("(save and load screen not built yet)", withClose: true));
        _menu.ResumeRequested += CloseMenu;
        _menu.PartyRequested += OpenParty;
        _menu.SavesRequested += () => OpenSaves(SaveScreenMode.SaveAndLoad);
        _menu.ReturnToStartRequested += ReturnToStart;
        _menu.QuitRequested += Quit;
    }

    private Control AddScreen(Control host, string path, Func<Control>? fallback)
    {
        var screen = ResourceLoader.Exists(path) ? GD.Load<PackedScene>(path).Instantiate<Control>() : fallback!();
        if (screen is IFlowScreen flowScreen)
        {
            flowScreen.Attach(this);
        }

        screen.Visible = false;
        host.AddChild(screen);
        return screen;
    }

    private void ShowContentError(ImmutableArray<RuleErrorDetail> problems)
    {
        var screen = GD.Load<PackedScene>(ContentErrorScreenPath).Instantiate<ContentErrorScreen>();
        _screenHost.AddChild(screen);
        screen.ShowProblems(problems);
        _contentError = screen;
        FocusIn(screen);
    }

    private static void FocusIn(Control? screen)
    {
        if (screen is null)
        {
            return;
        }

        Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(screen) || !screen.IsVisibleInTree())
            {
                return;
            }

            switch (screen)
            {
                case GameMenu menu:
                    menu.FocusFirst();
                    break;
                case ContentErrorScreen error:
                    error.FocusFirst();
                    break;
                default:
                    FirstButton(screen)?.GrabFocus();
                    break;
            }
        }).CallDeferred();
    }

    private static Button? FirstButton(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Button { Disabled: false, Visible: true } button)
            {
                return button;
            }

            if (FirstButton(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static void OnMainThread(Action action)
    {
        if (OS.GetThreadCallerId() == OS.GetMainThreadId())
        {
            action();
        }
        else
        {
            Callable.From(action).CallDeferred();
        }
    }

    private Control FallbackStart()
    {
        var root = CenteredPanel(out var box);
        box.AddChild(new Label { Text = Content!.Pack.Name, ThemeTypeVariation = "TitleLabel", HorizontalAlignment = HorizontalAlignment.Center });
        var newGame = new Button { Text = "New Game" };
        newGame.Pressed += StartNewGame;
        var load = new Button { Text = "Load Game" };
        load.Pressed += () => OpenSaves(SaveScreenMode.LoadOnly);
        box.AddChild(newGame);
        box.AddChild(load);
        return root;
    }

    private Control FallbackHandOff()
    {
        var root = CenteredPanel(out var box);
        var name = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        box.AddChild(name);
        box.AddChild(new Label { Text = "(fight screen not built yet)", ThemeTypeVariation = "MutedLabel" });
        var back = new Button { Text = "Return to Start" };
        back.Pressed += ReturnToStart;
        box.AddChild(back);
        SnapshotRendered += snapshot => name.Text = snapshot.HandOff?.FightName ?? string.Empty;
        return root;
    }

    private Control FallbackPanel(string text, bool withClose)
    {
        var root = CenteredPanel(out var box);
        box.AddChild(new Label { Text = text, ThemeTypeVariation = "MutedLabel" });
        if (withClose)
        {
            var close = new Button { Text = "Close" };
            close.Pressed += Back;
            box.AddChild(close);
        }

        return root;
    }

    private static Control CenteredPanel(out VBoxContainer box)
    {
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var panel = new PanelContainer();
        box = new VBoxContainer();
        panel.AddChild(box);
        center.AddChild(panel);
        root.AddChild(center);
        return root;
    }
}
