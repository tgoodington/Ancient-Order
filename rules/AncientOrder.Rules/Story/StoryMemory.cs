using System.Collections.Immutable;

namespace AncientOrder.Rules.Story;

/// <summary>A choice taken: the conversation it was in and its id.</summary>
public readonly record struct ChoiceKey(string ConversationId, string ChoiceId);

/// <summary>Compares choice keys by ordinal text, never by culture.</summary>
public sealed class ChoiceKeyComparer : IEqualityComparer<ChoiceKey>
{
    public static ChoiceKeyComparer Instance { get; } = new();

    public bool Equals(ChoiceKey x, ChoiceKey y) =>
        string.Equals(x.ConversationId, y.ConversationId, StringComparison.Ordinal)
        && string.Equals(x.ChoiceId, y.ChoiceId, StringComparison.Ordinal);

    public int GetHashCode(ChoiceKey key) =>
        HashCode.Combine(StringComparer.Ordinal.GetHashCode(key.ConversationId), StringComparer.Ordinal.GetHashCode(key.ChoiceId));
}

/// <summary>
/// What the game remembers of the story so far: flags set, choices taken, and conversations seen. The sets are lookup only and
/// never iterated for order; the save mapper (T10) sorts them. Updates return a new memory.
/// </summary>
public sealed record StoryMemory(ImmutableHashSet<string> Flags, ImmutableHashSet<ChoiceKey> Chosen, ImmutableHashSet<string> Seen)
{
    public static StoryMemory Empty { get; } = new(
        ImmutableHashSet<string>.Empty.WithComparer(StringComparer.Ordinal),
        ImmutableHashSet<ChoiceKey>.Empty.WithComparer(ChoiceKeyComparer.Instance),
        ImmutableHashSet<string>.Empty.WithComparer(StringComparer.Ordinal));

    public bool HasFlag(string flag) => Flags.Contains(flag);

    public bool HasChosen(string conversationId, string choiceId) => Chosen.Contains(new ChoiceKey(conversationId, choiceId));

    public bool HasSeen(string conversationId) => Seen.Contains(conversationId);

    public StoryMemory WithFlag(string flag) => this with { Flags = Flags.Add(flag) };

    public StoryMemory WithoutFlag(string flag) => this with { Flags = Flags.Remove(flag) };

    public StoryMemory WithChosen(string conversationId, string choiceId) => this with { Chosen = Chosen.Add(new ChoiceKey(conversationId, choiceId)) };

    public StoryMemory WithSeen(string conversationId) => this with { Seen = Seen.Add(conversationId) };
}
