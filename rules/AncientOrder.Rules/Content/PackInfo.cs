namespace AncientOrder.Rules.State;

/// <summary>Which content pack the content came from. Content built in C# is an unnamed pack that is not stand-in.</summary>
public sealed record PackInfo(string Id, string Name, bool StandIn)
{
    public static PackInfo Unnamed { get; } = new(string.Empty, string.Empty, false);
}
