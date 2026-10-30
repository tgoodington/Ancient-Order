namespace AncientOrder.Rules.Party;

/// <summary>How many fighters a lineup may hold. A lineup is ready for a fight when its size is within Min to Max.</summary>
public sealed record LineupLimits(int Min, int Max);
