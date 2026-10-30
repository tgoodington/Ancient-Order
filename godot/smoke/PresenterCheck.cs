using System.Collections.Immutable;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Content.Check;
using AncientOrder.Rules.Party;
using AncientOrder.Rules.Saves;
using AncientOrder.Rules.Session;
using AncientOrder.Ui;
using Godot;

namespace AncientOrder;

/// <summary>
/// Headless check of the party and save presenters (T18) against stand-in screens: lineup picks through the session, a refused pick, then save, load, delete and refused loads through the presenter.
/// Saves go under user://smoke/presenters/saves, which it empties before and after. Exits 0 on success, or 1 after printing the failing step.
/// </summary>
public partial class PresenterCheck : Node
{
    private const string SaveFolder = "user://smoke/presenters/saves";

    public override async void _Ready()
    {
        var step = "start";
        var folder = ProjectSettings.GlobalizePath(SaveFolder);
        try
        {
            Empty(folder);
            await Run(folder, s =>
            {
                step = s;
                GD.Print("presenters: " + s);
            });
            Empty(folder);
            GD.Print("PRESENTERS PASSED");
            GetTree().Quit(0);
        }
        catch (Exception ex)
        {
            Empty(folder);
            GD.PrintErr($"PRESENTERS FAILED at step '{step}': {ex.Message}");
            GetTree().Quit(1);
        }
    }

    private static void Need(bool ok, string what)
    {
        if (!ok)
        {
            throw new InvalidOperationException(what);
        }
    }

    private static void Empty(string folder)
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, true);
        }
    }

    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

    private async Task Until(Func<bool> done, string what)
    {
        for (var i = 0; i < 600 && !done(); i++)
        {
            await Frame();
        }

        Need(done(), "timed out waiting for " + what);
    }

    private sealed class FakeParty : IPartyScreen
    {
        public event Action<string, bool>? LineupToggled;

        public PartyView? Last { get; private set; }

        public int Renders { get; private set; }

        public void Render(PartyView view)
        {
            Last = view;
            Renders++;
        }

        public void Toggle(string id, bool on) => LineupToggled?.Invoke(id, on);
    }

    private sealed class FakeSaves : ISaveScreen
    {
        public event Action<int>? SaveRequested;

        public event Action<int>? LoadRequested;

        public event Action<int>? DeleteRequested;

        public ImmutableArray<SlotInfo> Slots { get; private set; }

        public bool Busy { get; private set; }

        public string? Error { get; private set; }

        public void Render(ImmutableArray<SlotInfo> slots, SaveScreenMode mode, bool busy, string? error)
        {
            Need(OS.GetThreadCallerId() == OS.GetMainThreadId(), "render off the main thread");
            Slots = slots;
            Busy = busy;
            Error = error;
        }

        public void Save(int slot) => SaveRequested?.Invoke(slot);

        public void Load(int slot) => LoadRequested?.Invoke(slot);

        public void Delete(int slot) => DeleteRequested?.Invoke(slot);
    }

    private async Task Run(string folder, Action<string> step)
    {
        var report = ContentCheck.Run(new GodotContentSource("res://content/standin"));
        var content = report.Content ?? throw new InvalidOperationException("stand-in pack failed");
        var session = new GameSession(content, new GameSessionOptions { Saves = new SaveStore(new FileSaveBackend(folder), content) });
        var flow = GD.Load<PackedScene>("res://main.tscn").Instantiate<ScreenFlow>();
        flow.AutoStart = false;
        AddChild(flow);
        flow.Start(content, session, report);
        var party = new FakeParty();
        var saves = new FakeSaves();
        _ = new PartyPresenter(flow, party);
        _ = new SavePresenter(flow, saves);
        LoadReport? loadedReport = null;
        flow.LoadCompleted += r => loadedReport = r;
        flow.StartNewGame();
        await Frame();

        step("party opens and renders from the session");
        flow.OpenMenu();
        flow.OpenParty();
        Need(party.Last is not null, "party not rendered");
        Need(party.Last!.Members.Any(m => m.CanFight), "stand-in new game has no fighter to pick");

        step("lineup toggles on and off through the session");
        var first = party.Last!.Members.First(m => m.CanFight).Id;
        party.Toggle(first, true);
        await Frame();
        Need(session.Snapshot.Party!.Lineup.Any(m => m.Id == first), "toggle on did not reach the session");
        Need(party.Last!.Lineup.Any(m => m.Id == first), "screen not redrawn after toggle on");
        party.Toggle(first, false);
        await Frame();
        Need(session.Snapshot.Party!.Lineup.All(m => m.Id != first), "toggle off did not reach the session");

        step("a refused pick is redrawn and does not change the lineup");
        var before = session.Snapshot.Party!.Lineup;
        var renders = party.Renders;
        party.Toggle("no_such_character", true);
        await Frame();
        Need(party.Renders > renders, "no redraw after a refusal");
        Need(session.Snapshot.Party!.Lineup.Length == before.Length, "refused pick changed the lineup");
        Need(flow.GetNode<Control>("TopLayer/ErrorBanner").Visible, "refusal not shown in the banner");
        flow.Back();

        step("saves open and list ten slots");
        flow.OpenSaves(SaveScreenMode.SaveAndLoad);
        await Until(() => !saves.Busy && saves.Slots.Length == 10, "slot list");
        Need(saves.Slots.All(s => s.Status == SlotStatus.Empty), "fresh slots not empty");

        step("save to slot 3 and refresh; a second request while busy is ignored");
        party.Toggle(first, true);
        await Frame();
        saves.Save(3);
        Need(saves.Busy, "not busy during save");
        saves.Load(3);
        await Until(() => !saves.Busy, "save");
        Need(saves.Error is null, "save error: " + saves.Error);
        Need(loadedReport is null, "a request during a busy save ran");
        Need(saves.Slots.Length == 10 && saves.Slots[2].Status == SlotStatus.Saved, "slot 3 not listed as saved");

        step("a damaged slot is listed and does not break the list");
        File.WriteAllText(System.IO.Path.Combine(folder, SaveStore.KeyFor(4) + ".json"), "{ not a save");
        flow.Back();
        flow.OpenSaves(SaveScreenMode.SaveAndLoad);
        await Until(() => !saves.Busy && saves.Slots.Length == 10, "relist");
        Need(saves.Slots[3].Status == SlotStatus.Corrupted, "damaged slot not marked");
        saves.Load(4);
        await Until(() => !saves.Busy, "damaged load");
        Need(saves.Error is not null, "damaged load shows no reason");
        Need(flow.IsSavesOpen, "damaged load closed the screen");

        step("load restores the lineup, closes the screens, and reports");
        flow.Back();
        flow.Back();
        flow.OpenMenu();
        flow.OpenParty();
        party.Toggle(first, false);
        await Frame();
        Need(session.Snapshot.Party!.Lineup.Length == 0, "lineup not cleared");
        flow.Back();
        flow.OpenSaves(SaveScreenMode.SaveAndLoad);
        await Until(() => !saves.Busy && saves.Slots.Length == 10, "relist");
        saves.Load(3);
        await Until(() => loadedReport is not null, "load");
        await Frame();
        Need(!flow.IsSavesOpen && !flow.IsMenuOpen, "screens not closed after load");
        Need(session.Snapshot.Party!.Lineup.Any(m => m.Id == first), "load did not restore the lineup");

        step("delete refreshes the list; an empty slot load is refused inline");
        flow.OpenMenu();
        flow.OpenSaves(SaveScreenMode.SaveAndLoad);
        await Until(() => !saves.Busy && saves.Slots.Length == 10, "relist");
        saves.Delete(3);
        await Until(() => !saves.Busy, "delete");
        Need(saves.Slots[2].Status == SlotStatus.Empty, "slot 3 not emptied");
        saves.Load(3);
        await Until(() => !saves.Busy, "empty load");
        Need(saves.Error is not null && flow.IsSavesOpen, "empty load not refused inline");

        step("RequestSlots lists on the main thread without touching the save screen");
        Result<ImmutableArray<SlotInfo>>? listed = null;
        var onMain = false;
        flow.Saves!.RequestSlots(r =>
        {
            onMain = OS.GetThreadCallerId() == OS.GetMainThreadId();
            listed = r;
        });
        await Until(() => listed is not null, "RequestSlots");
        Need(onMain && listed!.IsOk && listed.Value.Length == 10, "RequestSlots result wrong");

        step("load-only mode refuses a save");
        flow.Back();
        flow.CloseMenu();
        flow.OpenSaves(SaveScreenMode.LoadOnly);
        await Until(() => !saves.Busy && saves.Slots.Length == 10, "relist");
        saves.Save(5);
        Need(saves.Error is not null && saves.Slots[4].Status == SlotStatus.Empty, "load-only save not refused");

        flow.QueueFree();
    }
}
