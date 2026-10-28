namespace AncientOrder.Rules.State;

/// <summary>What changes in play for one character. Name, archetype, and personality are read from the catalog.</summary>
public sealed record CharacterState(double Affection, double Trust);
