using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Combat.Ai;
using AncientOrder.Rules.Tests.Fixtures;

namespace AncientOrder.Rules.Tests.Combat.Ai;

// Carried over from src/combat/behaviorTree/evaluator.test.ts (evaluate(combatant, state, config) becomes EnemyDecisions.Decide(combatant, state,
// archetypes, config), with the test archetypes as the catalog). The TypeScript default config (GROUP off) is EvaluatorConfig.Default.
// 'throws for unknown archetype ID' still throws (now InvalidOperationException). Tie-breaks use the combatant's own path; every combatant that
// names a scored archetype here already sets the path its old profile had (Elena Light, Lars Earth, Kade Fire), so no expected value changes.
// Two blocks at the end are new: the Basic behavior and the unknown-archetype error cases.
internal static class DecisionFixtures
{
    public static readonly ArchetypeCatalog Catalog = ArchetypeCatalog.Create(TestArchetypes.All).Value;

    public static readonly EvaluatorConfig GroupDisabled = new(false);

    public static readonly EvaluatorConfig GroupEnabled = new(true);

    public const string KadeId = "kade_rogue_outlaw";

    public const string ElenaId = "elena_loyal_scout";

    public const string LarsId = "lars_scheming_merchant";

    public static Combatant C(string id, string archetype = KadeId) => TestCombatants.Make(id, archetype);

    public static Combatant Pct(string id, double stamina, double maxStamina = 100, string archetype = KadeId) =>
        TestCombatants.Make(id, archetype) with { Stamina = stamina, MaxStamina = maxStamina };

    public static CombatAction Decide(Combatant self, CombatState state, EvaluatorConfig? config = null) =>
        EnemyDecisions.Decide(self, state, Catalog, config ?? EvaluatorConfig.Default);

    public static CombatState State(Combatant[] players, Combatant[] enemies, int round = 1) => TestCombatants.State(players, enemies, round);
}

public class EvaluateBasicCorrectness
{
    [Fact(DisplayName = "returns a valid CombatAction with matching combatantId")]
    public void Matching_combatant_id()
    {
        var self = DecisionFixtures.C("enemy_kade");
        var player = DecisionFixtures.C("player_1");
        var action = DecisionFixtures.Decide(self, DecisionFixtures.State([player], [self]));
        Assert.Equal("enemy_kade", action.CombatantId);
    }

    [Fact(DisplayName = "always returns a valid ActionType")]
    public void Valid_action_type()
    {
        var self = DecisionFixtures.C("enemy_kade");
        var player = DecisionFixtures.C("player_1");
        var action = DecisionFixtures.Decide(self, DecisionFixtures.State([player], [self]));
        Assert.Contains(action.Type, new[] { ActionType.Attack, ActionType.Defend, ActionType.Evade, ActionType.Special, ActionType.Group });
    }

    [Fact(DisplayName = "throws for unknown archetype ID")]
    public void Throws_for_unknown_archetype()
    {
        var self = DecisionFixtures.C("e1", "totally_unknown_archetype");
        var player = DecisionFixtures.C("p1");
        Assert.Throws<InvalidOperationException>(() => DecisionFixtures.Decide(self, DecisionFixtures.State([player], [self])));
    }

    [Fact(DisplayName = "uses default config (GROUP disabled) when config not provided")]
    public void Default_config()
    {
        var self = DecisionFixtures.C("e1") with { Energy = 0 };
        var player = DecisionFixtures.C("p1");
        var action = DecisionFixtures.Decide(self, DecisionFixtures.State([player], [self]));
        Assert.NotEqual(ActionType.Group, action.Type);
    }
}

public class EvaluateTargetValidity
{
    [Fact(DisplayName = "ATTACK targets a non-KO enemy (targetId is a valid enemy ID)")]
    public void Attack_targets_enemy()
    {
        var self = DecisionFixtures.C("e1");
        var player = DecisionFixtures.C("p1");
        var action = DecisionFixtures.Decide(self, DecisionFixtures.State([player], [self]));
        if (action.Type == ActionType.Attack)
        {
            Assert.Equal("p1", action.TargetId);
        }
    }

    [Fact(DisplayName = "EVADE has null targetId")]
    public void Evade_has_null_target()
    {
        var self = DecisionFixtures.Pct("e1", 10, 100, DecisionFixtures.ElenaId) with { ElementalPath = ElementalPath.Light };
        var player = DecisionFixtures.Pct("p1", 100, 100);
        var action = DecisionFixtures.Decide(self, DecisionFixtures.State([player], [self]));
        if (action.Type == ActionType.Evade)
        {
            Assert.Null(action.TargetId);
        }
    }

    [Fact(DisplayName = "never targets a KO enemy for ATTACK")]
    public void Never_targets_ko()
    {
        var self = DecisionFixtures.C("e1");
        var koEnemy = TestCombatants.Ko(DecisionFixtures.C("p1"));
        var liveEnemy = DecisionFixtures.Pct("p2", 80, 100);
        var action = DecisionFixtures.Decide(self, DecisionFixtures.State([koEnemy, liveEnemy], [self]));
        if (action.Type == ActionType.Attack)
        {
            Assert.NotEqual("p1", action.TargetId);
            Assert.Equal("p2", action.TargetId);
        }
    }

    [Fact(DisplayName = "when all enemies are KO, does not produce ATTACK")]
    public void All_enemies_ko()
    {
        var self = DecisionFixtures.C("e1");
        var koEnemy = TestCombatants.Ko(DecisionFixtures.C("p1"));
        var action = DecisionFixtures.Decide(self, DecisionFixtures.State([koEnemy], [self]));
        Assert.NotEqual(ActionType.Attack, action.Type);
        Assert.NotEqual(ActionType.Special, action.Type);
    }

    [Fact(DisplayName = "when all allies are KO, does not produce DEFEND")]
    public void All_allies_ko()
    {
        var self = DecisionFixtures.C("e1", DecisionFixtures.ElenaId) with { ElementalPath = ElementalPath.Light };
        var koAlly = TestCombatants.Ko(DecisionFixtures.C("e2"));
        var player = DecisionFixtures.C("p1");
        var action = DecisionFixtures.Decide(self, DecisionFixtures.State([player], [self, koAlly]));
        Assert.NotEqual(ActionType.Defend, action.Type);
    }
}

public class EvaluateSpecialActionEnergyHandling
{
    [Fact(DisplayName = "SPECIAL action includes energySegments equal to self energy")]
    public void Special_includes_energy()
    {
        var self = DecisionFixtures.C("e1") with { Energy = 5, MaxEnergy = 5, Stamina = 100, MaxStamina = 100 };
        var player = DecisionFixtures.Pct("p1", 10, 100);
        var action = DecisionFixtures.Decide(self, DecisionFixtures.State([player], [self]));
        if (action.Type == ActionType.Special)
        {
            Assert.Equal(5, action.EnergySegments);
        }
    }

    [Fact(DisplayName = "SPECIAL not produced when energy is 0")]
    public void No_special_at_zero_energy()
    {
        var self = DecisionFixtures.C("e1") with { Energy = 0 };
        var player = DecisionFixtures.C("p1");
        var action = DecisionFixtures.Decide(self, DecisionFixtures.State([player], [self]));
        Assert.NotEqual(ActionType.Special, action.Type);
    }
}

public class EvaluateGroupConfigFlag
{
    [Fact(DisplayName = "GROUP never produced when groupActionsEnabled=false")]
    public void Group_disabled()
    {
        var self = DecisionFixtures.C("e1", DecisionFixtures.ElenaId) with { ElementalPath = ElementalPath.Light };
        var ally = DecisionFixtures.C("e2") with { ElementalPath = ElementalPath.Fire };
        var player = DecisionFixtures.C("p1");

        for (var i = 0; i < 5; i++)
        {
            var action = DecisionFixtures.Decide(self, DecisionFixtures.State([player], [self, ally]), DecisionFixtures.GroupDisabled);
            Assert.NotEqual(ActionType.Group, action.Type);
        }
    }

    [Fact(DisplayName = "GROUP can be produced when groupActionsEnabled=true")]
    public void Group_enabled()
    {
        var self = DecisionFixtures.C("e1", DecisionFixtures.ElenaId) with { ElementalPath = ElementalPath.Light };
        var player = DecisionFixtures.C("p1");
        var action = DecisionFixtures.Decide(self, DecisionFixtures.State([player], [self]), DecisionFixtures.GroupEnabled);
        Assert.Contains(action.Type, new[] { ActionType.Attack, ActionType.Defend, ActionType.Evade, ActionType.Special, ActionType.Group });
    }
}

public class EvaluateDeterminism
{
    [Fact(DisplayName = "produces identical output for identical inputs (pure function)")]
    public void Pure_function()
    {
        var self = DecisionFixtures.C("e1") with { Energy = 2 };
        var player1 = DecisionFixtures.Pct("p1", 60, 100);
        var player2 = DecisionFixtures.Pct("p2", 30, 100);
        var state = DecisionFixtures.State([player1, player2], [self]);

        var action1 = DecisionFixtures.Decide(self, state, DecisionFixtures.GroupDisabled);
        var action2 = DecisionFixtures.Decide(self, state, DecisionFixtures.GroupDisabled);

        Assert.Equal(action1.Type, action2.Type);
        Assert.Equal(action1.TargetId, action2.TargetId);
        Assert.Equal(action1.EnergySegments, action2.EnergySegments);
    }

    [Fact(DisplayName = "different config produces potentially different output")]
    public void Config_respected()
    {
        var self = DecisionFixtures.C("e1");
        var player = DecisionFixtures.C("p1");
        var a1 = DecisionFixtures.Decide(self, DecisionFixtures.State([player], [self]), DecisionFixtures.GroupDisabled);
        var a2 = DecisionFixtures.Decide(self, DecisionFixtures.State([player], [self]), DecisionFixtures.GroupEnabled);
        Assert.NotNull(a1);
        Assert.NotNull(a2);
    }
}

public class EvaluateArchetypeDifferentiation
{
    [Fact(DisplayName = "Elena and Kade produce different decisions when ally is in danger")]
    public void Elena_and_kade_differ()
    {
        var weakPlayer = DecisionFixtures.Pct("p1", 10, 100);
        var ally = DecisionFixtures.Pct("ally_weak", 5, 100);

        var elena = DecisionFixtures.C("elena", DecisionFixtures.ElenaId) with { ElementalPath = ElementalPath.Light, Stamina = 100, MaxStamina = 100, Energy = 0 };
        var kade = DecisionFixtures.C("kade") with { ElementalPath = ElementalPath.Fire, Stamina = 100, MaxStamina = 100, Energy = 0 };

        var elenaAction = DecisionFixtures.Decide(elena, DecisionFixtures.State([weakPlayer], [elena, ally]));
        var kadeAction = DecisionFixtures.Decide(kade, DecisionFixtures.State([weakPlayer], [kade, ally]));

        var differentActions = elenaAction.Type != kadeAction.Type || elenaAction.TargetId != kadeAction.TargetId;
        Assert.True(differentActions);
    }

    [Fact(DisplayName = "Kade attacks a weak enemy more aggressively than Lars")]
    public void Kade_vs_lars()
    {
        var weakSlowEnemy = DecisionFixtures.Pct("p1", 5, 100) with { Speed = 2 };

        var kade = DecisionFixtures.C("kade") with { ElementalPath = ElementalPath.Fire, Speed = 20, Stamina = 100, MaxStamina = 100, Energy = 0 };
        var lars = DecisionFixtures.C("lars", DecisionFixtures.LarsId) with { ElementalPath = ElementalPath.Earth, Speed = 20, Stamina = 100, MaxStamina = 100, Energy = 0 };

        var kadeAction = DecisionFixtures.Decide(kade, DecisionFixtures.State([weakSlowEnemy], [kade]));
        var larsAction = DecisionFixtures.Decide(lars, DecisionFixtures.State([weakSlowEnemy], [lars]));

        var valid = new[] { ActionType.Attack, ActionType.Special, ActionType.Evade, ActionType.Defend };
        Assert.Contains(kadeAction.Type, valid);
        Assert.Contains(larsAction.Type, valid);
    }

    [Fact(DisplayName = "all 3 archetypes produce valid actions in a standard 1v1")]
    public void All_three_valid()
    {
        var player = DecisionFixtures.Pct("p1", 70, 100);

        (string Id, string Archetype, ElementalPath Path)[] overrides =
        [
            ("e1", DecisionFixtures.ElenaId, ElementalPath.Light),
            ("e2", DecisionFixtures.LarsId, ElementalPath.Earth),
            ("e3", DecisionFixtures.KadeId, ElementalPath.Fire),
        ];

        foreach (var (id, archetype, path) in overrides)
        {
            var self = DecisionFixtures.C(id, archetype) with { ElementalPath = path, Stamina = 80, MaxStamina = 100 };
            var action = DecisionFixtures.Decide(self, DecisionFixtures.State([player], [self]));
            Assert.Equal(id, action.CombatantId);
            Assert.Contains(action.Type, new[] { ActionType.Attack, ActionType.Defend, ActionType.Evade, ActionType.Special, ActionType.Group });
        }
    }
}

public class EvaluateRankCoefficientIntegration
{
    [Fact(DisplayName = "high-rank and low-rank produce consistent actions (both valid, deterministic)")]
    public void Both_valid()
    {
        var player = DecisionFixtures.Pct("p1", 50, 100);

        var lowRank = DecisionFixtures.C("e_low") with { Rank = 1.0 };
        var highRank = DecisionFixtures.C("e_high") with { Rank = 10.0 };

        var lowAction = DecisionFixtures.Decide(lowRank, DecisionFixtures.State([player], [lowRank]));
        var highAction = DecisionFixtures.Decide(highRank, DecisionFixtures.State([player], [highRank]));

        Assert.Equal("e_low", lowAction.CombatantId);
        Assert.Equal("e_high", highAction.CombatantId);
    }
}

public class EvaluateEdgeCases
{
    [Fact(DisplayName = "single enemy remaining: targets it for ATTACK")]
    public void Single_enemy()
    {
        var self = DecisionFixtures.C("e1") with { Energy = 0 };
        var lastEnemy = DecisionFixtures.Pct("p_last", 40, 100);
        var action = DecisionFixtures.Decide(self, DecisionFixtures.State([lastEnemy], [self]));
        if (action.Type == ActionType.Attack)
        {
            Assert.Equal("p_last", action.TargetId);
        }
    }

    [Fact(DisplayName = "3v3 scenario runs without error for all 3 NPC archetypes")]
    public void Three_v_three()
    {
        Combatant[] players = [DecisionFixtures.Pct("p1", 80, 100), DecisionFixtures.Pct("p2", 60, 100), DecisionFixtures.Pct("p3", 40, 100)];

        var elena = DecisionFixtures.Pct("elena", 90, 100, DecisionFixtures.ElenaId) with { ElementalPath = ElementalPath.Light };
        var lars = DecisionFixtures.Pct("lars", 85, 100, DecisionFixtures.LarsId) with { ElementalPath = ElementalPath.Earth };
        var kade = DecisionFixtures.Pct("kade", 70, 100, DecisionFixtures.KadeId) with { ElementalPath = ElementalPath.Fire };

        Combatant[] enemies = [elena, lars, kade];
        var state = DecisionFixtures.State(players, enemies);

        foreach (var enemy in enemies)
        {
            var action = DecisionFixtures.Decide(enemy, state);
            Assert.Equal(enemy.Id, action.CombatantId);
        }
    }

    [Fact(DisplayName = "all candidates at negative scores — still picks best (least negative)")]
    public void Negative_scores()
    {
        var self = DecisionFixtures.Pct("e1", 5, 100) with { Energy = 0 };
        var strongPlayer = DecisionFixtures.Pct("p1", 100, 100) with { Power = 100, Speed = 50 };
        var action = DecisionFixtures.Decide(self, DecisionFixtures.State([strongPlayer], [self]));
        Assert.NotNull(action);
        Assert.Equal("e1", action.CombatantId);
    }
}

// New (T15): a Basic archetype attacks the first living opponent and nothing else.
public class DecideBasicBehavior
{
    private const string BasicId = "warrior";

    private static Combatant Basic(string id) => DecisionFixtures.C(id, BasicId);

    [Fact(DisplayName = "a basic archetype attacks the first living opponent")]
    public void Attacks_first_living_opponent()
    {
        var self = Basic("e1");
        var action = DecisionFixtures.Decide(self, DecisionFixtures.State([DecisionFixtures.C("p1"), DecisionFixtures.C("p2")], [self]));
        Assert.Equal(new CombatAction("e1", ActionType.Attack, "p1", null), action);
    }

    [Fact(DisplayName = "a basic archetype skips knocked-out opponents")]
    public void Skips_ko()
    {
        var self = Basic("e1");
        var action = DecisionFixtures.Decide(
            self,
            DecisionFixtures.State([TestCombatants.Ko(DecisionFixtures.C("p1")), DecisionFixtures.C("p2")], [self]));
        Assert.Equal("p2", action.TargetId);
    }

    [Fact(DisplayName = "a basic archetype in the player party attacks the first living enemy, never an ally")]
    public void Player_party_attacks_enemies()
    {
        var self = Basic("p1");
        var ally = DecisionFixtures.C("p2");
        var enemy = DecisionFixtures.C("e1");
        var action = DecisionFixtures.Decide(self, DecisionFixtures.State([self, ally], [enemy]));
        Assert.Equal(new CombatAction("p1", ActionType.Attack, "e1", null), action);
    }

    [Fact(DisplayName = "a basic archetype ignores its energy, stamina, and the group flag")]
    public void Ignores_energy_and_flags()
    {
        var self = Basic("e1") with { Energy = 5, Stamina = 3, MaxStamina = 100 };
        var state = DecisionFixtures.State([DecisionFixtures.Pct("p1", 5, 100)], [self]);
        var expected = new CombatAction("e1", ActionType.Attack, "p1", null);
        Assert.Equal(expected, DecisionFixtures.Decide(self, state, DecisionFixtures.GroupDisabled));
        Assert.Equal(expected, DecisionFixtures.Decide(self, state, DecisionFixtures.GroupEnabled));
    }

    [Fact(DisplayName = "a basic archetype with every opponent knocked out has no target")]
    public void No_living_opponent()
    {
        var self = Basic("e1");
        var action = DecisionFixtures.Decide(self, DecisionFixtures.State([TestCombatants.Ko(DecisionFixtures.C("p1"))], [self]));
        Assert.Equal(ActionType.Attack, action.Type);
        Assert.Null(action.TargetId);
    }

    [Fact(DisplayName = "every basic test archetype decides without a profile")]
    public void All_basic_archetypes()
    {
        foreach (var id in new[] { "enemy", "kade_bold_outlaw", "lars_cautious_merchant", "warrior", "scout", "mage" })
        {
            var self = DecisionFixtures.C("e1", id);
            var action = DecisionFixtures.Decide(self, DecisionFixtures.State([DecisionFixtures.C("p1")], [self]));
            Assert.Equal(new CombatAction("e1", ActionType.Attack, "p1", null), action);
        }
    }
}

// New (T15): an archetype missing from the catalog at decision time is a bug and throws (content validation prevents it).
public class DecideUnknownArchetype
{
    [Fact(DisplayName = "an archetype missing from the catalog throws and names the archetype")]
    public void Throws_and_names_it()
    {
        var self = DecisionFixtures.C("e1", "totally_unknown_archetype");
        var state = DecisionFixtures.State([DecisionFixtures.C("p1")], [self]);
        var error = Assert.Throws<InvalidOperationException>(() => DecisionFixtures.Decide(self, state));
        Assert.Contains("totally_unknown_archetype", error.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "a combatant with no archetype id throws")]
    public void Null_archetype_throws()
    {
        var self = DecisionFixtures.C("e1", null!);
        var state = DecisionFixtures.State([DecisionFixtures.C("p1")], [self]);
        Assert.Throws<InvalidOperationException>(() => DecisionFixtures.Decide(self, state));
    }

    [Fact(DisplayName = "an unknown archetype does not fall back to a stub action")]
    public void No_stub_fallback()
    {
        var self = DecisionFixtures.C("e1", "stub");
        var state = DecisionFixtures.State([DecisionFixtures.C("p1")], [self]);
        Assert.Throws<InvalidOperationException>(() => DecisionFixtures.Decide(self, state, DecisionFixtures.GroupEnabled));
    }
}
