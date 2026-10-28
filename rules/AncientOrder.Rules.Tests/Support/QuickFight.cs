using System.Collections.Immutable;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Session;
using AncientOrder.Rules.State;
using AncientOrder.Rules.Tests.Fixtures;

namespace AncientOrder.Rules.Tests.Support;

/// <summary>
/// Port of tests/helpers/quickFight.ts. One side is made thin (stamina 1, power 1) and the other strong (stamina 5000, power 200),
/// so a fight ends within a few rounds. The player's side comes from the player and the lineup, so each outcome has its own content.
/// </summary>
public static class QuickFight
{
    public const string WinEncounterId = "encounter_quick_win";
    public const string LossEncounterId = "encounter_quick_loss";

    private static readonly ImmutableArray<string> PlayerSide = [TestSessions.Hero, TestCast.ElenaId, TestCast.LarsId, TestCast.KadeId];

    public static GameContent WinContent { get; } = Build(WinEncounterId, enemiesAreWeak: true);

    public static GameContent LossContent { get; } = Build(LossEncounterId, enemiesAreWeak: false);

    /// <summary>A session with the lineup set and the quick win (or quick loss) encounter started.</summary>
    public static GameSession Start(bool win) =>
        TestSessions.InCombat(win ? WinEncounterId : LossEncounterId, win ? WinContent : LossContent);

    /// <summary>A session in a new game with the lineup set, ready for StartEncounter of the quick win (or loss) encounter.</summary>
    public static GameSession Ready(bool win) => TestSessions.WithLineup(win ? WinContent : LossContent);

    /// <summary>Declares an attack from every fighter still up on the first enemy still up, advances, and repeats until the fight ends or the cap is hit.</summary>
    public static CombatStatus FightToEnd(GameSession session, int maxRounds = 60)
    {
        var status = CombatStatus.Active;
        for (var i = 0; i < maxRounds && status == CombatStatus.Active; i++)
        {
            var view = session.GetCombatView().Value;
            var target = view.Fighters.FirstOrDefault(f => IsEnemy(f.Id) && f.Stance == FighterStance.Active);
            if (target is not null)
            {
                var orders = view.Fighters
                    .Where(f => !IsEnemy(f.Id) && f.Stance == FighterStance.Active)
                    .Select(f => TestSessions.Attack(f.Id, target.Id))
                    .ToList();
                session.DeclareOrders(orders);
            }

            var advanced = session.AdvanceRound();
            Assert.True(advanced.IsOk, advanced.IsOk ? string.Empty : "advanceRound failed: " + advanced.Error.Code);
            status = advanced.Value.Status;
        }

        return status;
    }

    private static bool IsEnemy(string id) => !PlayerSide.Contains(id);

    private static GameContent Build(string encounterId, bool enemiesAreWeak)
    {
        static CombatProfile Thin(CombatProfile p) => p with { Rank = 1, Stamina = 1, Power = 1 };
        static CombatProfile Strong(CombatProfile p) => p with { Stamina = 5000, Power = 200 };

        var playerSide = enemiesAreWeak ? (Func<CombatProfile, CombatProfile>)Strong : Thin;
        var characters = TestCast.All.Select(c => c with { Combat = playerSide(c.Combat!) }).ToImmutableArray();
        var enemies = SessionEncounters.TestEncounter.Enemies
            .Select(e => enemiesAreWeak ? e with { Stamina = 1, Power = 1 } : e with { Stamina = 5000, Power = 200 })
            .ToImmutableArray();
        return TestSessions.Content(
            playerSide(TestSessions.HeroProfile),
            characters,
            [new EncounterDefinition(encounterId, "Quick fight", enemies)]);
    }
}
