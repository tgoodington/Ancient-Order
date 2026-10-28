using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Saves;
using AncientOrder.Rules.Session;
using Godot;

namespace AncientOrder;

/// <summary>
/// Headless smoke run: first builds the game's composition root (GameHost) and starts a game through it, without touching its saves.
/// Then plays a fight through the session under Godot's .NET runtime, saving and loading mid-fight.
/// Uses STAND-IN content and saves only under user://smoke/saves, which it empties before and after.
/// Exits 0 on success, or 1 after printing the failing step.
/// </summary>
public partial class SmokeRun : Node
{
    private const string SaveFolder = "user://smoke/saves";
    private const long SavedAt = 1_700_000_000_000;

    public override async void _Ready()
    {
        var folder = ProjectSettings.GlobalizePath(SaveFolder);
        var step = "start";
        try
        {
            EmptyFolder(folder);
            await Play(folder, s => step = s);
            EmptyFolder(folder);
            GD.Print("SMOKE PASSED");
            GetTree().Quit(0);
        }
        catch (Exception ex)
        {
            TryEmpty(folder);
            GD.PrintErr($"SMOKE FAILED at step '{step}': {ex.Message}");
            GetTree().Quit(1);
        }
    }

    private static async Task Play(string folder, Action<string> setStep)
    {
        void Step(string name)
        {
            setStep(name);
            GD.Print("smoke: " + name);
        }

        Step("GameHost");
        var host = new GameHost();
        Need(host.Session.NewGame());
        if (host.Session.Snapshot.Version != 1 || host.Session.Snapshot.Game?.Player.Name != host.Content.Settings.PlayerName)
        {
            throw new InvalidOperationException("the game host did not start a game with the stand-in player");
        }

        var content = StandInContent.Create();
        var session = new GameSession(content, new GameSessionOptions
        {
            Saves = new SaveStore(new FileSaveBackend(folder), content),
            OnListenerError = error => GD.PushError(error.ToString()),
        });

        Step("NewGame");
        Need(session.NewGame());

        Step("SetLineup");
        Need(session.SetLineup([StandInContent.CompanionAId, StandInContent.CompanionBId]));

        Step("StartEncounter");
        Need(session.StartEncounter(StandInContent.EncounterId));

        Step("DeclareOrders");
        var view = Need(session.GetCombatView());
        var orders = view.Fighters
            .Where(f => f.Id != StandInContent.EnemyId)
            .Select(f => new CombatAction(f.Id, ActionType.Attack, StandInContent.EnemyId, null))
            .ToList();
        var declared = Need(session.DeclareOrders(orders));
        if (declared.Accepted.Length != orders.Count)
        {
            throw new InvalidOperationException($"{declared.Rejected.Length} of {orders.Count} orders were rejected");
        }

        Step("AdvanceRound");
        var after = Need(session.AdvanceRound());
        if (after.Status != CombatStatus.Active)
        {
            throw new InvalidOperationException($"the fight ended after one round ({after.Status}); a mid-fight save needs it still active");
        }

        Step("SaveToSlot(1)");
        var saved = session.Snapshot.Game ?? throw new InvalidOperationException("no game after the round");
        Need(await session.SaveToSlot(1));

        Step("LoadFromSlot(1)");
        Need(await session.LoadFromSlot(1));

        Step("compare loaded game with saved game");
        var loaded = session.Snapshot.Game ?? throw new InvalidOperationException("no game after the load");
        if (loaded.Combat is null)
        {
            throw new InvalidOperationException("the loaded game has no fight");
        }

        var before = Bytes(saved, content);
        var afterLoad = Bytes(loaded, content);
        if (!before.AsSpan().SequenceEqual(afterLoad))
        {
            throw new InvalidOperationException("the loaded game differs from the saved game");
        }

        Step("DeleteSlot(1)");
        Need(await session.DeleteSlot(1));
    }

    private static byte[] Bytes(AncientOrder.Rules.State.GameState game, AncientOrder.Rules.State.GameContent content) =>
        Need(SaveFormat.Write(1, SaveConstants.KindManual, game, content, SavedAt));

    private static T Need<T>(Result<T> result) =>
        result.IsOk ? result.Value : throw new InvalidOperationException($"{result.Error.Code}: {result.Error.Message}");

    private static void EmptyFolder(string folder)
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, true);
        }

        Directory.CreateDirectory(folder);
    }

    private static void TryEmpty(string folder)
    {
        try
        {
            EmptyFolder(folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            GD.PrintErr("smoke: could not empty " + folder + ": " + ex.Message);
        }
    }
}
