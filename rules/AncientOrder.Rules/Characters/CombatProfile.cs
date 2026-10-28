using AncientOrder.Rules.Combat;

namespace AncientOrder.Rules.State;

/// <summary>A fighter's base combat stats from content. Reaction skills are not part of it; the player's side derives them from reaction progress (ADR-054).</summary>
public sealed record CombatProfile(
    double Rank,
    double Stamina,
    double Power,
    double Speed,
    ElementalPath ElementalPath,
    AscensionLevel AscensionLevel = AscensionLevel.Level0)
{
    /// <summary>Returns the reason the profile is invalid, or null when it is valid.</summary>
    public string? Validate()
    {
        if (!double.IsFinite(Rank) || Rank <= 0)
        {
            return "rank must be a finite number above 0";
        }

        if (!double.IsFinite(Stamina) || Stamina <= 0)
        {
            return "stamina must be a finite number above 0";
        }

        if (!double.IsFinite(Power) || Power < 0)
        {
            return "power must be a finite number of 0 or more";
        }

        if (!double.IsFinite(Speed) || Speed < 0)
        {
            return "speed must be a finite number of 0 or more";
        }

        if (!Enum.IsDefined(ElementalPath))
        {
            return "elementalPath is not a known path";
        }

        if (!Enum.IsDefined(AscensionLevel))
        {
            return "ascensionLevel must be 0 to 3";
        }

        return null;
    }
}
