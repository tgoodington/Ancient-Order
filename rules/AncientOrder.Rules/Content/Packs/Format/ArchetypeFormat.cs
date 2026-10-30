namespace AncientOrder.Rules.Content.Packs.Format;

/// <summary>An archetype with a profile decides by scoring; one without attacks the first living opponent. Reactions are how its characters react to a choice's shift.</summary>
public sealed record ArchetypeFormat(string Id, DecisionProfileFormat? Profile = null, ReactionsFormat? Reactions = null);

public sealed record ReactionsFormat(
    LeaningsFormat Leanings,
    double Minimum,
    double AffectionPerPoint,
    double TrustPerPoint,
    ReactionLinesFormat Approve,
    ReactionLinesFormat Disapprove);

public sealed record LeaningsFormat(
    double? Patience = null,
    double? Empathy = null,
    double? Cunning = null,
    double? Logic = null,
    double? Kindness = null,
    double? Charisma = null);

public sealed record ReactionLinesFormat(string Clear, string? Slight = null, string? Strong = null);

public sealed record DecisionProfileFormat(BaseScoresFormat BaseScores, Dictionary<string, double> FactorWeights);

public sealed record BaseScoresFormat(double Attack, double Defend, double Evade, double Special, double Group);
