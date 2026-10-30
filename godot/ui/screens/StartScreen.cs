using AncientOrder.Rules.Saves;
using Godot;

namespace AncientOrder.Ui;

/// <summary>
/// The first screen: the game's title with New Game, Load Game and Quit. Load stays disabled, with a "No saves yet" note, until the slot list shows
/// at least one filled or damaged slot (damaged slots count so the player can reach them to delete). The list is read whenever the screen is shown and
/// again after the save screen closes, through the save presenter. If the list cannot be read, Load stays enabled (the save screen shows its own error) and no note appears. The stand-in marker comes from ScreenFlow's top layer.
/// </summary>
public partial class StartScreen : Control, IFlowScreen
{
    private ScreenFlow _flow = null!;
    private Label _title = null!;
    private Button _newGame = null!;
    private Button _load = null!;
    private Label _noSaves = null!;
    private int _refreshes;
    private bool _watching;

    public bool LoadEnabled => !_load.Disabled;

    public bool NoSavesShown => _noSaves.Visible;

    public void Attach(ScreenFlow flow) => _flow = flow;

    public override void _Ready()
    {
        _title = GetNode<Label>("Center/Column/Title");
        _newGame = GetNode<Button>("Center/Column/NewGame");
        _load = GetNode<Button>("Center/Column/LoadRow/Load");
        _noSaves = GetNode<Label>("Center/Column/LoadRow/NoSaves");
        var quit = GetNode<Button>("Center/Column/Quit");
        _title.Text = ProjectSettings.GetSetting("application/config/name").AsString();
        _newGame.Pressed += _flow.StartNewGame;
        _load.Pressed += () => _flow.OpenSaves(SaveScreenMode.LoadOnly);
        quit.Pressed += () => GetTree().Quit();
        _flow.SavesOpened += OnSavesOpened;
        VisibilityChanged += OnVisibilityChanged;
        OnVisibilityChanged();
    }

    public override void _ExitTree()
    {
        _refreshes++;
        if (_flow is not null)
        {
            _flow.SavesOpened -= OnSavesOpened;
        }
    }

    private void OnVisibilityChanged()
    {
        if (Visible)
        {
            RefreshLoad();
        }
    }

    private void OnSavesOpened(SaveScreenMode mode)
    {
        if (_watching || _flow.SaveScreen is null)
        {
            return;
        }

        _watching = true;
        _flow.SaveScreen.VisibilityChanged += () =>
        {
            if (!_flow.SaveScreen.Visible && Visible)
            {
                RefreshLoad();
            }
        };
    }

    private void RefreshLoad()
    {
        var ticket = ++_refreshes;
        if (_flow.Saves is null)
        {
            SetLoad(true, resolved: false);
            return;
        }

        SetLoad(false, resolved: false);
        _flow.Saves.RequestSlots(listed =>
        {
            if (!IsInstanceValid(this) || ticket != _refreshes || !IsInsideTree())
            {
                return;
            }

            var available = !listed.IsOk || listed.Value.Any(slot => slot.Status != SlotStatus.Empty);
            SetLoad(available, resolved: listed.IsOk);
        });
    }

    private void SetLoad(bool available, bool resolved)
    {
        _load.Disabled = !available;
        _noSaves.Visible = resolved && !available;
    }
}
