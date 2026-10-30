using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Reactions;

/// <summary>How a character carries themselves toward the player.</summary>
public enum Demeanor
{
    Warm,
    Neutral,
    Wary,
    Cold,
}

public static class DemeanorRules
{
    /// <summary>From affection plus trust against the settings cutoffs: Warm at or above WarmAt, Cold at or below ColdAt, Wary at or below WaryAt, otherwise Neutral.</summary>
    public static Demeanor Of(GameSettings settings, CharacterState relationship)
    {
        var total = relationship.Affection + relationship.Trust;
        var cutoffs = settings.Demeanor;
        if (total >= cutoffs.WarmAt)
        {
            return Demeanor.Warm;
        }

        if (total <= cutoffs.ColdAt)
        {
            return Demeanor.Cold;
        }

        return total <= cutoffs.WaryAt ? Demeanor.Wary : Demeanor.Neutral;
    }
}
