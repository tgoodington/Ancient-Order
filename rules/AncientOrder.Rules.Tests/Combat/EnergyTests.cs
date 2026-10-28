using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Tests.Fixtures;

namespace AncientOrder.Rules.Tests.Combat;

// Carried over from src/combat/energy.test.ts. getStartingSegments and resetRoundEnergy are not ported (T5), so their describe blocks
// and the cross-system block (which calls both) are dropped. toBeCloseTo(x) with no precision is 2 decimals, kept as precision 2.
public class AddEnergySegmentsLevel0NoAccumulationBonus
{
    [Fact(DisplayName = "actionSuccess at level 0 gains 1.0 segment")]
    public void Action_success()
    {
        var combatant = TestCombatants.Fighter() with { Energy = 0, AscensionLevel = AscensionLevel.Level0 };
        Assert.Equal(1.0, Energy.AddEnergySegments(combatant, EnergyEvent.Action, true).Energy, 2);
    }

    [Fact(DisplayName = "actionSuccess failure at level 0 gains 0.5 segments")]
    public void Action_failure()
    {
        var combatant = TestCombatants.Fighter() with { Energy = 0, AscensionLevel = AscensionLevel.Level0 };
        Assert.Equal(0.5, Energy.AddEnergySegments(combatant, EnergyEvent.Action, false).Energy, 2);
    }

    [Fact(DisplayName = "reactionSuccess at level 0 gains 0.5 segments")]
    public void Reaction_success()
    {
        var combatant = TestCombatants.Fighter() with { Energy = 0, AscensionLevel = AscensionLevel.Level0 };
        Assert.Equal(0.5, Energy.AddEnergySegments(combatant, EnergyEvent.Reaction, true).Energy, 2);
    }

    [Fact(DisplayName = "reactionSuccess failure at level 0 gains 0.25 segments")]
    public void Reaction_failure()
    {
        var combatant = TestCombatants.Fighter() with { Energy = 0, AscensionLevel = AscensionLevel.Level0 };
        Assert.Equal(0.25, Energy.AddEnergySegments(combatant, EnergyEvent.Reaction, false).Energy, 2);
    }
}

public class AddEnergySegmentsLevel1Plus25PercentAccumulationBonus
{
    [Fact(DisplayName = "actionSuccess at level 1 gains 1.25 segments (1.0 × 1.25)")]
    public void Action_success()
    {
        var combatant = TestCombatants.Fighter() with { Energy = 0, AscensionLevel = AscensionLevel.Level1 };
        Assert.Equal(1.25, Energy.AddEnergySegments(combatant, EnergyEvent.Action, true).Energy, 2);
    }

    [Fact(DisplayName = "actionSuccess failure at level 1 gains 0.625 segments (0.5 × 1.25)")]
    public void Action_failure()
    {
        var combatant = TestCombatants.Fighter() with { Energy = 0, AscensionLevel = AscensionLevel.Level1 };
        Assert.Equal(0.625, Energy.AddEnergySegments(combatant, EnergyEvent.Action, false).Energy, 2);
    }

    [Fact(DisplayName = "reactionSuccess at level 1 gains 0.625 segments (0.5 × 1.25)")]
    public void Reaction_success()
    {
        var combatant = TestCombatants.Fighter() with { Energy = 0, AscensionLevel = AscensionLevel.Level1 };
        Assert.Equal(0.625, Energy.AddEnergySegments(combatant, EnergyEvent.Reaction, true).Energy, 2);
    }

    [Fact(DisplayName = "reactionSuccess failure at level 1 gains 0.3125 segments (0.25 × 1.25)")]
    public void Reaction_failure()
    {
        var combatant = TestCombatants.Fighter() with { Energy = 0, AscensionLevel = AscensionLevel.Level1 };
        Assert.Equal(0.3125, Energy.AddEnergySegments(combatant, EnergyEvent.Reaction, false).Energy, 2);
    }
}

public class AddEnergySegmentsLevel2Plus25PercentAccumulationBonus
{
    [Fact(DisplayName = "actionSuccess at level 2 gains 1.25 segments (1.0 × 1.25)")]
    public void Action_success()
    {
        var combatant = TestCombatants.Fighter() with { Energy = 0, AscensionLevel = AscensionLevel.Level2 };
        Assert.Equal(1.25, Energy.AddEnergySegments(combatant, EnergyEvent.Action, true).Energy, 2);
    }

    [Fact(DisplayName = "reactionSuccess failure at level 2 gains 0.3125 segments (0.25 × 1.25)")]
    public void Reaction_failure()
    {
        var combatant = TestCombatants.Fighter() with { Energy = 0, AscensionLevel = AscensionLevel.Level2 };
        Assert.Equal(0.3125, Energy.AddEnergySegments(combatant, EnergyEvent.Reaction, false).Energy, 2);
    }
}

public class AddEnergySegmentsLevel3Plus50PercentAccumulationBonus
{
    [Fact(DisplayName = "actionSuccess at level 3 gains 1.5 segments (1.0 × 1.5)")]
    public void Action_success()
    {
        var combatant = TestCombatants.Fighter() with { Energy = 0, AscensionLevel = AscensionLevel.Level3 };
        Assert.Equal(1.5, Energy.AddEnergySegments(combatant, EnergyEvent.Action, true).Energy, 2);
    }

    [Fact(DisplayName = "actionSuccess failure at level 3 gains 0.75 segments (0.5 × 1.5)")]
    public void Action_failure()
    {
        var combatant = TestCombatants.Fighter() with { Energy = 0, AscensionLevel = AscensionLevel.Level3 };
        Assert.Equal(0.75, Energy.AddEnergySegments(combatant, EnergyEvent.Action, false).Energy, 2);
    }

    [Fact(DisplayName = "reactionSuccess at level 3 gains 0.75 segments (0.5 × 1.5)")]
    public void Reaction_success()
    {
        var combatant = TestCombatants.Fighter() with { Energy = 0, AscensionLevel = AscensionLevel.Level3 };
        Assert.Equal(0.75, Energy.AddEnergySegments(combatant, EnergyEvent.Reaction, true).Energy, 2);
    }

    [Fact(DisplayName = "reactionSuccess failure at level 3 gains 0.375 segments (0.25 × 1.5)")]
    public void Reaction_failure()
    {
        var combatant = TestCombatants.Fighter() with { Energy = 0, AscensionLevel = AscensionLevel.Level3 };
        Assert.Equal(0.375, Energy.AddEnergySegments(combatant, EnergyEvent.Reaction, false).Energy, 2);
    }
}

public class AddEnergySegmentsAccumulationBehavior
{
    [Fact(DisplayName = "energy accumulates across multiple calls")]
    public void Accumulates()
    {
        var combatant = TestCombatants.Fighter() with { Energy = 0, AscensionLevel = AscensionLevel.Level0 };
        combatant = Energy.AddEnergySegments(combatant, EnergyEvent.Action, true);
        combatant = Energy.AddEnergySegments(combatant, EnergyEvent.Reaction, true);
        combatant = Energy.AddEnergySegments(combatant, EnergyEvent.Action, false);
        Assert.Equal(2.0, combatant.Energy, 2);
    }

    [Fact(DisplayName = "does not mutate the original combatant")]
    public void Does_not_mutate()
    {
        var combatant = TestCombatants.Fighter() with { Energy = 0 };
        Energy.AddEnergySegments(combatant, EnergyEvent.Action, true);
        Assert.Equal(0, combatant.Energy);
    }

    [Fact(DisplayName = "returns a new combatant reference")]
    public void New_reference()
    {
        var combatant = TestCombatants.Fighter() with { Energy = 0 };
        var result = Energy.AddEnergySegments(combatant, EnergyEvent.Action, true);
        Assert.NotSame(combatant, result);
    }
}

public class CheckAscensionAdvance
{
    private static Combatant With(double energy, AscensionLevel level) => TestCombatants.Fighter() with { Energy = energy, AscensionLevel = level };

    [Fact(DisplayName = "energy below 35 stays at level 0")]
    public void Below_35() => Assert.Equal(AscensionLevel.Level0, Energy.CheckAscensionAdvance(With(34, AscensionLevel.Level0)).AscensionLevel);

    [Fact(DisplayName = "energy at exactly 35 advances to level 1")]
    public void At_35() => Assert.Equal(AscensionLevel.Level1, Energy.CheckAscensionAdvance(With(35, AscensionLevel.Level0)).AscensionLevel);

    [Fact(DisplayName = "energy above 35 but below 95 stays at level 1")]
    public void Between_35_and_95() => Assert.Equal(AscensionLevel.Level1, Energy.CheckAscensionAdvance(With(94, AscensionLevel.Level1)).AscensionLevel);

    [Fact(DisplayName = "energy at exactly 95 advances to level 2")]
    public void At_95() => Assert.Equal(AscensionLevel.Level2, Energy.CheckAscensionAdvance(With(95, AscensionLevel.Level1)).AscensionLevel);

    [Fact(DisplayName = "energy above 95 but below 180 stays at level 2")]
    public void Between_95_and_180() => Assert.Equal(AscensionLevel.Level2, Energy.CheckAscensionAdvance(With(179, AscensionLevel.Level2)).AscensionLevel);

    [Fact(DisplayName = "energy at exactly 180 advances to level 3")]
    public void At_180() => Assert.Equal(AscensionLevel.Level3, Energy.CheckAscensionAdvance(With(180, AscensionLevel.Level2)).AscensionLevel);

    [Fact(DisplayName = "energy well above 180 stays at level 3")]
    public void Above_180() => Assert.Equal(AscensionLevel.Level3, Energy.CheckAscensionAdvance(With(300, AscensionLevel.Level3)).AscensionLevel);

    [Fact(DisplayName = "returns same reference when level does not change")]
    public void Same_reference()
    {
        var combatant = With(10, AscensionLevel.Level0);
        Assert.Same(combatant, Energy.CheckAscensionAdvance(combatant));
    }

    [Fact(DisplayName = "returns new reference when level advances")]
    public void New_reference()
    {
        var combatant = With(35, AscensionLevel.Level0);
        Assert.NotSame(combatant, Energy.CheckAscensionAdvance(combatant));
    }

    [Fact(DisplayName = "does not mutate the original combatant on advancement")]
    public void Does_not_mutate()
    {
        var combatant = With(35, AscensionLevel.Level0);
        Energy.CheckAscensionAdvance(combatant);
        Assert.Equal(AscensionLevel.Level0, combatant.AscensionLevel);
    }

    [Fact(DisplayName = "can advance multiple levels at once (level 0 → 3 from 180+ energy)")]
    public void Advances_multiple_levels() => Assert.Equal(AscensionLevel.Level3, Energy.CheckAscensionAdvance(With(200, AscensionLevel.Level0)).AscensionLevel);
}
