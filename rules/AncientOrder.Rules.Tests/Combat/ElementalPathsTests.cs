using System.Collections.Immutable;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Tests.Fixtures;

namespace AncientOrder.Rules.Tests.Combat;

// Carried over from src/combat/elementalPaths.test.ts. Buff types such as 'parrySR_boost' and 'dodgeSR_debuff' become the BuffStat
// (ParrySR, DodgeSR, BlockSR); a debuff is a buff with a negative modifier. The 'power_boost' fixture buff has no duration (T5).
public class ElementalPathConfig
{
    private static readonly ElementalPath[] AllPaths = [ElementalPath.Fire, ElementalPath.Water, ElementalPath.Air, ElementalPath.Earth, ElementalPath.Shadow, ElementalPath.Light];

    [Fact(DisplayName = "contains all 6 elemental paths")]
    public void Contains_all_six()
    {
        Assert.Equal(6, Enum.GetValues<ElementalPath>().Length);
        foreach (var path in AllPaths)
        {
            Assert.NotNull(ElementalPaths.Config(path));
        }
    }

    [Fact(DisplayName = "classifies Fire, Air, Light as reaction paths")]
    public void Reaction_paths()
    {
        Assert.Equal(PathType.Reaction, ElementalPaths.Config(ElementalPath.Fire).Type);
        Assert.Equal(PathType.Reaction, ElementalPaths.Config(ElementalPath.Air).Type);
        Assert.Equal(PathType.Reaction, ElementalPaths.Config(ElementalPath.Light).Type);
    }

    [Fact(DisplayName = "classifies Water, Earth, Shadow as action paths")]
    public void Action_paths()
    {
        Assert.Equal(PathType.Action, ElementalPaths.Config(ElementalPath.Water).Type);
        Assert.Equal(PathType.Action, ElementalPaths.Config(ElementalPath.Earth).Type);
        Assert.Equal(PathType.Action, ElementalPaths.Config(ElementalPath.Shadow).Type);
    }

    [Fact(DisplayName = "assigns correct defenseBoost per path")]
    public void Defense_boost()
    {
        Assert.Equal(DefenseType.Parry, ElementalPaths.Config(ElementalPath.Fire).DefenseBoost);
        Assert.Equal(DefenseType.Dodge, ElementalPaths.Config(ElementalPath.Water).DefenseBoost);
        Assert.Equal(DefenseType.Dodge, ElementalPaths.Config(ElementalPath.Air).DefenseBoost);
        Assert.Equal(DefenseType.Block, ElementalPaths.Config(ElementalPath.Earth).DefenseBoost);
        Assert.Equal(DefenseType.Parry, ElementalPaths.Config(ElementalPath.Shadow).DefenseBoost);
        Assert.Equal(DefenseType.Block, ElementalPaths.Config(ElementalPath.Light).DefenseBoost);
    }

    [Fact(DisplayName = "assigns correct specialForces per path (matches defenseBoost)")]
    public void Special_forces()
    {
        Assert.Equal(DefenseType.Parry, ElementalPaths.Config(ElementalPath.Fire).SpecialForces);
        Assert.Equal(DefenseType.Dodge, ElementalPaths.Config(ElementalPath.Water).SpecialForces);
        Assert.Equal(DefenseType.Dodge, ElementalPaths.Config(ElementalPath.Air).SpecialForces);
        Assert.Equal(DefenseType.Block, ElementalPaths.Config(ElementalPath.Earth).SpecialForces);
        Assert.Equal(DefenseType.Parry, ElementalPaths.Config(ElementalPath.Shadow).SpecialForces);
        Assert.Equal(DefenseType.Block, ElementalPaths.Config(ElementalPath.Light).SpecialForces);
    }
}

public class ApplyPathBuff
{
    [Fact(DisplayName = "Fire path adds a parrySR_boost buff to the combatant")]
    public void Fire()
    {
        var result = ElementalPaths.ApplyPathBuff(TestCombatants.Fighter() with { ElementalPath = ElementalPath.Fire }, ElementalPath.Fire);

        Assert.Single(result.ActiveBuffs);
        var buff = result.ActiveBuffs[0];
        Assert.Equal(BuffStat.ParrySR, buff.Stat);
        Assert.Equal("Fire", buff.Source);
        Assert.True(buff.Modifier > 0);
    }

    [Fact(DisplayName = "Air path adds a dodgeSR_boost buff to the combatant")]
    public void Air()
    {
        var result = ElementalPaths.ApplyPathBuff(TestCombatants.Fighter() with { ElementalPath = ElementalPath.Air }, ElementalPath.Air);

        Assert.Single(result.ActiveBuffs);
        var buff = result.ActiveBuffs[0];
        Assert.Equal(BuffStat.DodgeSR, buff.Stat);
        Assert.Equal("Air", buff.Source);
        Assert.True(buff.Modifier > 0);
    }

    [Fact(DisplayName = "Light path adds a blockSR_boost buff to the combatant")]
    public void Light()
    {
        var result = ElementalPaths.ApplyPathBuff(TestCombatants.Fighter() with { ElementalPath = ElementalPath.Light }, ElementalPath.Light);

        Assert.Single(result.ActiveBuffs);
        var buff = result.ActiveBuffs[0];
        Assert.Equal(BuffStat.BlockSR, buff.Stat);
        Assert.Equal("Light", buff.Source);
        Assert.True(buff.Modifier > 0);
    }

    [Fact(DisplayName = "buff modifier is positive (SR increase)")]
    public void Positive_modifier()
    {
        var combatant = TestCombatants.Fighter();

        Assert.Equal(0.1, ElementalPaths.ApplyPathBuff(combatant, ElementalPath.Fire).ActiveBuffs[0].Modifier);
        Assert.Equal(0.1, ElementalPaths.ApplyPathBuff(combatant, ElementalPath.Air).ActiveBuffs[0].Modifier);
        Assert.Equal(0.1, ElementalPaths.ApplyPathBuff(combatant, ElementalPath.Light).ActiveBuffs[0].Modifier);
    }

    [Fact(DisplayName = "does not mutate the original combatant")]
    public void Does_not_mutate()
    {
        var combatant = TestCombatants.Fighter();
        var originalBuffCount = combatant.ActiveBuffs.Length;
        ElementalPaths.ApplyPathBuff(combatant, ElementalPath.Fire);
        Assert.Equal(originalBuffCount, combatant.ActiveBuffs.Length);
    }

    [Fact(DisplayName = "returns a new combatant reference")]
    public void New_reference()
    {
        var combatant = TestCombatants.Fighter();
        Assert.NotSame(combatant, ElementalPaths.ApplyPathBuff(combatant, ElementalPath.Fire));
    }

    [Fact(DisplayName = "appends buff without removing existing buffs")]
    public void Appends()
    {
        var existingBuff = new Buff(BuffStat.Power, "test", 10);
        var combatant = TestCombatants.Fighter() with { ActiveBuffs = [existingBuff] };
        var result = ElementalPaths.ApplyPathBuff(combatant, ElementalPath.Fire);

        Assert.Equal(2, result.ActiveBuffs.Length);
        Assert.Equal(existingBuff, result.ActiveBuffs[0]);
    }

    [Fact(DisplayName = "is idempotent — re-applying the same path buff does not stack (ADR-053)")]
    public void Idempotent()
    {
        var once = ElementalPaths.ApplyPathBuff(TestCombatants.Fighter(), ElementalPath.Fire);
        var twice = ElementalPaths.ApplyPathBuff(once, ElementalPath.Fire);
        var thrice = ElementalPaths.ApplyPathBuff(twice, ElementalPath.Fire);

        Assert.Single(thrice.ActiveBuffs);
        Assert.Equal(0.1, thrice.ActiveBuffs[0].Modifier);
        Assert.Same(once, twice);
    }
}

public class ApplyPathDebuff
{
    [Fact(DisplayName = "Water path adds a dodgeSR debuff to the target")]
    public void Water()
    {
        var result = ElementalPaths.ApplyPathDebuff(TestCombatants.Fighter(), ElementalPath.Water);

        Assert.Single(result.ActiveBuffs);
        var debuff = result.ActiveBuffs[0];
        Assert.Equal(BuffStat.DodgeSR, debuff.Stat);
        Assert.Equal("Water", debuff.Source);
        Assert.True(debuff.Modifier < 0);
    }

    [Fact(DisplayName = "Earth path adds a blockSR debuff to the target")]
    public void Earth()
    {
        var result = ElementalPaths.ApplyPathDebuff(TestCombatants.Fighter(), ElementalPath.Earth);

        Assert.Single(result.ActiveBuffs);
        var debuff = result.ActiveBuffs[0];
        Assert.Equal(BuffStat.BlockSR, debuff.Stat);
        Assert.Equal("Earth", debuff.Source);
        Assert.True(debuff.Modifier < 0);
    }

    [Fact(DisplayName = "Shadow path adds a parrySR debuff to the target")]
    public void Shadow()
    {
        var result = ElementalPaths.ApplyPathDebuff(TestCombatants.Fighter(), ElementalPath.Shadow);

        Assert.Single(result.ActiveBuffs);
        var debuff = result.ActiveBuffs[0];
        Assert.Equal(BuffStat.ParrySR, debuff.Stat);
        Assert.Equal("Shadow", debuff.Source);
        Assert.True(debuff.Modifier < 0);
    }

    [Fact(DisplayName = "debuff modifier is negative (SR reduction)")]
    public void Negative_modifier()
    {
        var target = TestCombatants.Fighter();

        Assert.Equal(-0.1, ElementalPaths.ApplyPathDebuff(target, ElementalPath.Water).ActiveBuffs[0].Modifier);
        Assert.Equal(-0.1, ElementalPaths.ApplyPathDebuff(target, ElementalPath.Earth).ActiveBuffs[0].Modifier);
        Assert.Equal(-0.1, ElementalPaths.ApplyPathDebuff(target, ElementalPath.Shadow).ActiveBuffs[0].Modifier);
    }

    [Fact(DisplayName = "does not mutate the original target")]
    public void Does_not_mutate()
    {
        var target = TestCombatants.Fighter();
        var originalBuffCount = target.ActiveBuffs.Length;
        ElementalPaths.ApplyPathDebuff(target, ElementalPath.Water);
        Assert.Equal(originalBuffCount, target.ActiveBuffs.Length);
    }

    [Fact(DisplayName = "returns a new combatant reference")]
    public void New_reference()
    {
        var target = TestCombatants.Fighter();
        Assert.NotSame(target, ElementalPaths.ApplyPathDebuff(target, ElementalPath.Earth));
    }

    [Fact(DisplayName = "appends debuff without removing existing buffs")]
    public void Appends()
    {
        var existingBuff = new Buff(BuffStat.Power, "test", 10);
        var target = TestCombatants.Fighter() with { ActiveBuffs = [existingBuff] };
        var result = ElementalPaths.ApplyPathDebuff(target, ElementalPath.Shadow);

        Assert.Equal(2, result.ActiveBuffs.Length);
        Assert.Equal(existingBuff, result.ActiveBuffs[0]);
    }

    [Fact(DisplayName = "is idempotent — re-applying the same path debuff does not stack (ADR-053)")]
    public void Idempotent()
    {
        var once = ElementalPaths.ApplyPathDebuff(TestCombatants.Fighter(), ElementalPath.Earth);
        var twice = ElementalPaths.ApplyPathDebuff(once, ElementalPath.Earth);
        var thrice = ElementalPaths.ApplyPathDebuff(twice, ElementalPath.Earth);

        Assert.Single(thrice.ActiveBuffs);
        Assert.Equal(-0.1, thrice.ActiveBuffs[0].Modifier);
        Assert.Same(once, twice);
    }
}

public class GetSpecialForceDefense
{
    [Fact(DisplayName = "Fire forces parry defense")]
    public void Fire() => Assert.Equal(DefenseType.Parry, ElementalPaths.GetSpecialForceDefense(ElementalPath.Fire));

    [Fact(DisplayName = "Water forces dodge defense")]
    public void Water() => Assert.Equal(DefenseType.Dodge, ElementalPaths.GetSpecialForceDefense(ElementalPath.Water));

    [Fact(DisplayName = "Air forces dodge defense")]
    public void Air() => Assert.Equal(DefenseType.Dodge, ElementalPaths.GetSpecialForceDefense(ElementalPath.Air));

    [Fact(DisplayName = "Earth forces block defense")]
    public void Earth() => Assert.Equal(DefenseType.Block, ElementalPaths.GetSpecialForceDefense(ElementalPath.Earth));

    [Fact(DisplayName = "Shadow forces parry defense")]
    public void Shadow() => Assert.Equal(DefenseType.Parry, ElementalPaths.GetSpecialForceDefense(ElementalPath.Shadow));

    [Fact(DisplayName = "Light forces block defense")]
    public void Light() => Assert.Equal(DefenseType.Block, ElementalPaths.GetSpecialForceDefense(ElementalPath.Light));

    [Fact(DisplayName = "returns a valid DefenseType for every path")]
    public void Valid_for_every_path()
    {
        ImmutableArray<DefenseType> valid = [DefenseType.Block, DefenseType.Dodge, DefenseType.Parry, DefenseType.Defenseless];
        foreach (var path in Enum.GetValues<ElementalPath>())
        {
            Assert.Contains(ElementalPaths.GetSpecialForceDefense(path), valid);
        }
    }
}

public class ReactionPathsOnlyBuffSelfActionPathsOnlyDebuffTarget
{
    [Fact(DisplayName = "applying Fire buff produces a positive modifier (self-enhancement)")]
    public void Fire_buff()
    {
        var self = TestCombatants.Fighter() with { ElementalPath = ElementalPath.Fire };
        var buffed = ElementalPaths.ApplyPathBuff(self, ElementalPath.Fire);
        var buff = buffed.ActiveBuffs.First(b => b.Source == "Fire");
        Assert.True(buff.Modifier > 0);
    }

    [Fact(DisplayName = "applying Water debuff produces a negative modifier (target reduction)")]
    public void Water_debuff()
    {
        var debuffed = ElementalPaths.ApplyPathDebuff(TestCombatants.Fighter(), ElementalPath.Water);
        var debuff = debuffed.ActiveBuffs.First(b => b.Source == "Water");
        Assert.True(debuff.Modifier < 0);
    }

    [Fact(DisplayName = "reaction path buff targets the correct defense type (Fire → parry)")]
    public void Fire_targets_parry()
    {
        var buffed = ElementalPaths.ApplyPathBuff(TestCombatants.Fighter(), ElementalPath.Fire);
        Assert.Equal(BuffStat.ParrySR, buffed.ActiveBuffs[0].Stat);
    }

    [Fact(DisplayName = "action path debuff targets the correct defense type (Earth → block)")]
    public void Earth_targets_block()
    {
        var debuffed = ElementalPaths.ApplyPathDebuff(TestCombatants.Fighter(), ElementalPath.Earth);
        Assert.Equal(BuffStat.BlockSR, debuffed.ActiveBuffs[0].Stat);
    }
}
