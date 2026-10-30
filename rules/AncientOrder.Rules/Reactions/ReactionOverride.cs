namespace AncientOrder.Rules.Reactions;

/// <summary>
/// A choice's say over one participant's reaction. Silence removes it; otherwise Text (with optional Affection and Trust) replaces it.
/// It fires whether or not the choice shifts personality.
/// </summary>
public sealed record ReactionOverride(string CharacterId, bool Silence = false, string? Text = null, double? Affection = null, double? Trust = null);
