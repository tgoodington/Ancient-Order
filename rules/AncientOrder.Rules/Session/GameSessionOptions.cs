using AncientOrder.Rules.Common;

namespace AncientOrder.Rules.Session;

/// <summary>
/// Outside inputs the session hands to the rules. OnListenerError receives any exception a listener throws.
/// Saves is the store behind the save slots; left null, the session uses a memory backend over its own content
/// (a SaveStore holds its content, so there is no default instance here: the options do not know the content).
/// </summary>
public sealed record GameSessionOptions
{
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;

    public IIdSource IdSource { get; init; } = new GuidIdSource();

    public ISeedSource SeedSource { get; init; } = new SystemSeedSource();

    public Action<Exception>? OnListenerError { get; init; }

    public AncientOrder.Rules.Saves.SaveStore? Saves { get; init; }
}
