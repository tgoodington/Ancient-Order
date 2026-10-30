using System.Collections.Immutable;
using AncientOrder.Rules.Content.Check;
using AncientOrder.Rules.Saves;
using AncientOrder.Rules.Session;
using AncientOrder.Ui;
using Godot;

namespace AncientOrder;

/// <summary>
/// Headless check of the hand-off placeholder (T22): on the stand-in pack it marks the exit ready, sets a lineup, triggers the exit, and checks the screen shows the placeholder heading,
/// the fight's name and the lineup in order; Return to Start goes to the start screen, and a saved hand-off loads back onto the screen. Saves go under user://smoke/handoff/saves.
/// Exits 0 on success, or 1 after printing the failing step. Pass "--shot=path" after "--" to write a screenshot (needs a window).
/// </summary>
public partial class HandOffCheck : Node
{
    private const string ExitId = "standin_exit";
    private const string SaveFolder = "user://smoke/handoff/saves";

    // The proven route in rules/AncientOrder.Rules.Tests/Playthrough/StandInRoute.cs, without its save and load branch.
    private static readonly (string Target, bool IsSpot, double X, double Z, string[] Choices)[] Route =
    [
        ("standin_elder", false, 20, 16, ["warm"]),
        ("standin_elder", false, 20, 16, ["favor"]),
        ("standin_gate", true, 36, 8, ["leave"]),
        ("standin_merchant", false, 12, 20, ["gift"]),
        ("standin_merchant", false, 12, 20, ["gift"]),
        ("standin_merchant", false, 12, 20, ["join"]),
        ("standin_smith", false, 20, 24, ["slight"]),
        ("standin_smith", false, 20, 24, ["strong"]),
        ("standin_smith", false, 20, 24, ["traveler"]),
        ("standin_smith", false, 20, 24, ["move"]),
        ("standin_smith", false, 28, 18, ["bell"]),
        ("standin_recruit", false, 35, 25, ["invite"]),
        ("standin_guard", false, 34, 20, ["pass"]),
        ("standin_guard", false, 34, 20, ["apologize"]),
        ("standin_guard", false, 34, 20, ["pass"]),
        ("standin_companion_b", false, 12, 23, ["part"]),
    ];

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
                GD.Print("handoff: " + s);
            });
            Empty(folder);
            GD.Print("HANDOFF PASSED");
            GetTree().Quit(0);
        }
        catch (Exception ex)
        {
            Empty(folder);
            GD.PrintErr($"HANDOFF FAILED at step '{step}': {ex.Message}");
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

    private async Task Run(string folder, Action<string> step)
    {
        step("reach the hand-off on the stand-in pack");
        var report = ContentCheck.Run(new GodotContentSource("res://content/standin"));
        var content = report.Content ?? throw new InvalidOperationException("stand-in pack failed");
        var session = new GameSession(content, new GameSessionOptions { Saves = new SaveStore(new FileSaveBackend(folder), content) });
        var flow = GD.Load<PackedScene>("res://main.tscn").Instantiate<ScreenFlow>();
        flow.AutoStart = false;
        AddChild(flow);
        flow.Start(content, session, report);
        await Frame();
        flow.StartNewGame();
        await Frame();
        foreach (var (target, isSpot, x, z, choices) in Route)
        {
            Need(session.MovePlayer(x, z).IsOk, "could not walk to " + target);
            var opened = isSpot ? session.TriggerSpot(target) : session.TriggerPerson(target);
            Need(opened.IsOk, $"could not trigger {target}: {(opened.IsOk ? string.Empty : opened.Error.Message)}");
            foreach (var choice in choices)
            {
                Need(session.Choose(choice).IsOk, $"could not choose {choice} in {target}");
            }

            for (var guard = 0; session.Snapshot.Conversation is not null; guard++)
            {
                Need(guard < 20 && session.Continue().IsOk, "conversation did not finish: " + target);
            }
        }

        Need(session.MovePlayer(20, 3).IsOk, "could not walk to the scene");
        for (var guard = 0; session.Snapshot.Conversation is not null; guard++)
        {
            Need(guard < 20 && session.Continue().IsOk, "walk-over scene did not finish");
        }

        Need(session.MovePlayer(20, 35).IsOk, "could not walk to the exit");
        Need(session.TriggerSpot(ExitId).IsOk, "exit refused");
        Need(session.Choose("mark_ready").IsOk, "mark_ready refused");
        for (var guard = 0; session.Snapshot.Conversation is not null; guard++)
        {
            Need(guard < 20 && session.Continue().IsOk, "exit talk did not finish");
        }

        var set = session.SetLineup(["standin_companion_a", "standin_recruit", "standin_merchant"]);
        Need(set.IsOk, "lineup refused: " + (set.IsOk ? string.Empty : set.Error.Message));
        var handed = session.TriggerSpot(ExitId);
        Need(handed.IsOk, "hand-off refused: " + (handed.IsOk ? string.Empty : handed.Error.Message));
        await Frame();
        await Frame();

        step("the placeholder replaces the town");
        var screen = flow.HandOffScreen ?? throw new InvalidOperationException("no hand-off screen");
        Need(screen is HandOffScreen, "hand-off screen is not the real scene");
        Need(flow.IsAtHandOff && screen.Visible && !flow.Stage.Visible, "not showing the hand-off");
        var texts = Labels(screen);
        Need(texts.Any(t => t.Contains("PLACEHOLDER") && t.Contains("Sprint 8")), "placeholder heading missing");
        var view = session.Snapshot.HandOff!;
        Need(texts.Contains(view.FightName) && view.FightName.Length > 0, "fight name missing");
        foreach (var member in view.Lineup)
        {
            Need(texts.Contains(member.Name), "lineup name missing: " + member.Name);
        }

        Need(view.Lineup.Length == 3, "unexpected lineup size");
        GD.Print($"handoff: shows '{view.FightName}' with {string.Join(", ", view.Lineup.Select(m => m.Name))}");
        await Shot(flow);

        step("esc menu is available and save works at the hand-off");
        flow._UnhandledInput(new InputEventAction { Action = "ui_menu", Pressed = true });
        Need(flow.IsMenuOpen, "menu not open at the hand-off");
        flow._UnhandledInput(new InputEventAction { Action = "ui_menu", Pressed = true });
        Need(!flow.IsMenuOpen, "menu not closed");
        Need((await session.SaveToSlot(1)).IsOk, "save refused");

        step("return to start");
        FindButton(screen).EmitSignal(BaseButton.SignalName.Pressed);
        await Frame();
        Need(flow.IsAtStart && !screen.Visible, "did not return to the start screen");

        step("a loaded hand-off save shows the screen again");
        var loaded = await session.LoadFromSlot(1);
        Need(loaded.IsOk, "load refused");
        flow.NotifyLoaded(loaded.Value);
        await Frame();
        Need(flow.IsAtHandOff && screen.Visible, "load did not return to the hand-off");
        Need(Labels(screen).Contains(view.FightName), "fight name missing after load");

        flow.QueueFree();
    }

    private async Task Shot(ScreenFlow flow)
    {
        var path = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--shot="))?["--shot=".Length..];
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path) || DisplayServer.GetName() == "headless")
        {
            return;
        }

        await Frame();
        await Frame();
        flow.GetViewport().GetTexture().GetImage().SavePng(path);
    }

    private static ImmutableHashSet<string> Labels(Node node)
    {
        var found = ImmutableHashSet.CreateBuilder<string>();
        void Walk(Node n)
        {
            if (n is Label label)
            {
                found.Add(label.Text);
            }

            foreach (var child in n.GetChildren())
            {
                Walk(child);
            }
        }

        Walk(node);
        return found.ToImmutable();
    }

    private static Button FindButton(Node node) =>
        node.FindChildren("*", "Button", true, false).OfType<Button>().FirstOrDefault(b => b.Text == "Return to Start")
        ?? throw new InvalidOperationException("no Return to Start button");
}
