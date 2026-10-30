namespace AncientOrder.Rules.Reactions;

/// <summary>Which way a reaction leans: toward the player (Warmer), away (Cooler), or neither (a written line with no change).</summary>
public enum TellDirection
{
    Warmer,
    Cooler,
    Neutral,
}

/// <summary>How visible a reaction is, so a screen can show it without numbers.</summary>
public enum TellStrength
{
    Slight,
    Clear,
    Strong,
}

/// <summary>Where a reaction came from: the archetype's liking or disliking, or a line the choice wrote.</summary>
public enum ReactionKind
{
    Approve,
    Disapprove,
    Written,
}
