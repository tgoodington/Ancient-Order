namespace AncientOrder.Rules.Combat;

public static class Energy
{
    public static Combatant AddEnergySegments(Combatant combatant, EnergyEvent eventType, bool success)
    {
        var gain = Formulas.CalculateEnergyGain(eventType, success, combatant.AscensionLevel);
        return combatant with { Energy = combatant.Energy + gain };
    }

    /// <summary>Treats the combatant's Energy as the total accumulated segments for the threshold check; returns the same combatant when the level is unchanged.</summary>
    public static Combatant CheckAscensionAdvance(Combatant combatant)
    {
        var newLevel = Formulas.CalculateAscensionLevel(combatant.Energy);
        return newLevel == combatant.AscensionLevel ? combatant : combatant with { AscensionLevel = newLevel };
    }

    /// <summary>Spending after a Special (pipeline.ts: energy minus segments used, floored at 0).</summary>
    public static Combatant SpendEnergy(Combatant combatant, double segments) =>
        combatant with { Energy = Math.Max(0, combatant.Energy - segments) };
}
