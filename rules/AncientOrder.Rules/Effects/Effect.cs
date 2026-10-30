using AncientOrder.Rules.Personality;

namespace AncientOrder.Rules.Effects;

/// <summary>
/// Something a choice does to the game, applied in written order. Later tasks add personality shifts
/// and the hand-off as more cases.
/// </summary>
public abstract record Effect
{
    private protected Effect()
    {
    }

    /// <summary>Changes a character's affection and/or trust toward the player (the totals stay within -100 to 100).</summary>
    public sealed record Relationship(string CharacterId, double? Affection, double? Trust) : Effect;

    /// <summary>Adds a character to the party. Already in the party changes nothing.</summary>
    public sealed record Join(string CharacterId) : Effect;

    /// <summary>Removes a character from the party and the lineup. Not in the party changes nothing.</summary>
    public sealed record Leave(string CharacterId) : Effect;

    /// <summary>Shifts the player's personality; the result always stays within 5-35% and sums to 100 (PersonalityRules.Adjust).</summary>
    public sealed record Shift(PersonalityAdjustment Adjustment) : Effect;

    /// <summary>Sets a flag. Already set changes nothing.</summary>
    public sealed record SetFlag(string FlagId) : Effect;

    /// <summary>Clears a flag. Not set changes nothing.</summary>
    public sealed record ClearFlag(string FlagId) : Effect;

    /// <summary>Leaves the town for a fight (an encounter id). Refused unless the lineup is ready for that fight; it must be a choice's last effect and the choice has no destination.</summary>
    public sealed record HandOff(string FightId) : Effect;
}
