using AncientOrder.Rules.Personality;

namespace AncientOrder.Rules.Reactions;

/// <summary>How much a character of an archetype cares about each trait: positive likes a rise, negative dislikes it. Unwritten traits weigh nothing.</summary>
public sealed record ReactionLeanings(
    double Patience = 0,
    double Empathy = 0,
    double Cunning = 0,
    double Logic = 0,
    double Kindness = 0,
    double Charisma = 0)
{
    public double Of(Trait trait) => trait switch
    {
        Trait.Patience => Patience,
        Trait.Empathy => Empathy,
        Trait.Cunning => Cunning,
        Trait.Logic => Logic,
        Trait.Kindness => Kindness,
        Trait.Charisma => Charisma,
        _ => 0,
    };
}

/// <summary>Body-language lines by strength. Slight and Strong fall back to Clear. Text may use {name}.</summary>
public sealed record ReactionLines(string Clear, string? Slight = null, string? Strong = null)
{
    public string For(TellStrength strength) => strength switch
    {
        TellStrength.Slight => Slight ?? Clear,
        TellStrength.Strong => Strong ?? Clear,
        _ => Clear,
    };
}

/// <summary>
/// An archetype's reactions, from content. A choice's score for a character is the sum of leaning times written shift over the traits;
/// a score below Minimum (in size) draws no reaction. Approve is used for a positive score, Disapprove for a negative one.
/// </summary>
public sealed record ReactionProfile(
    ReactionLeanings Leanings,
    double Minimum,
    double AffectionPerPoint,
    double TrustPerPoint,
    ReactionLines Approve,
    ReactionLines Disapprove);
