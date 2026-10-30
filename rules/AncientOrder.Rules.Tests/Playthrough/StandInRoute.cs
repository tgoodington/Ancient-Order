using System.Collections.Immutable;

namespace AncientOrder.Rules.Tests.Playthrough;

/// <summary>One thing the player does on the stand-in route.</summary>
internal abstract record RouteStep
{
    /// <summary>Walks to a point, triggers the person or spot there, then takes the choices in order (Continue is pressed whenever a step has nothing to pick).</summary>
    internal sealed record Talk(string Target, bool IsSpot, double X, double Z, ImmutableArray<string> Choices) : RouteStep;

    /// <summary>Walks to a point; entering a walk-over spot opens its scene, which is read to the end.</summary>
    internal sealed record Walk(double X, double Z) : RouteStep;

    internal sealed record Lineup(ImmutableArray<string?> Ids) : RouteStep;

    internal sealed record Save(int Slot) : RouteStep;

    internal sealed record Load(int Slot) : RouteStep;
}

/// <summary>
/// The scripted route through the stand-in pack (godot/content/standin), in one place so a stand-in edit touches one file.
/// The Elder opens at a different step once seen, so his first greeting can be taken only once: the written-reaction choice is
/// played on a branch (save, take it, load), then the main route plays the warm choice.
/// </summary>
internal static class StandInRoute
{
    public const string Pack = "godot/content/standin";

    public const string Elder = "standin_elder";
    public const string Smith = "standin_smith";
    public const string Merchant = "standin_merchant";
    public const string Guard = "standin_guard";
    public const string Recruit = "standin_recruit";
    public const string CompanionA = "standin_companion_a";
    public const string CompanionB = "standin_companion_b";
    public const string MarketBell = "standin_market_bell";
    public const string Gate = "standin_gate";
    public const string Exit = "standin_exit";
    public const string Encounter = "standin_encounter";

    private static RouteStep.Talk Person(string id, double x, double z, params string[] choices) => new(id, false, x, z, [.. choices]);

    private static RouteStep.Talk Spot(string id, double x, double z, params string[] choices) => new(id, true, x, z, [.. choices]);

    public static ImmutableArray<RouteStep> Steps { get; } =
    [
        new RouteStep.Save(1),
        Person(Elder, 20, 16, "override"),
        new RouteStep.Load(1),
        Person(Elder, 20, 16, "warm"),
        Person(Elder, 20, 16, "favor"),
        Spot(Gate, 36, 8, "leave"),
        Person(Merchant, 12, 20, "gift"),
        Person(Merchant, 12, 20, "gift"),
        Person(Merchant, 12, 20, "join"),
        Person(Smith, 20, 24, "slight"),
        Person(Smith, 20, 24, "strong"),
        Person(Smith, 20, 24, "traveler"),
        Person(Smith, 20, 24, "move"),
        Person(Smith, 28, 18, "bell"),
        Person(Recruit, 35, 25, "invite"),
        Person(Guard, 34, 20, "pass"),
        Person(Guard, 34, 20, "apologize"),
        Person(Guard, 34, 20, "pass"),
        Person(CompanionB, 12, 23, "part"),
        new RouteStep.Walk(20, 3),
        Spot(Exit, 20, 35, "mark_ready"),
        new RouteStep.Lineup([CompanionA, Recruit, Merchant]),
        Spot(Exit, 20, 35),
    ];
}
