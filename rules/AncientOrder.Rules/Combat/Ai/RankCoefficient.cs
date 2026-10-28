namespace AncientOrder.Rules.Combat.Ai;

public static class RankCoefficient
{
    private const double RankFloor = 0.2;
    private const double RankMax = 10.0;

    /// <summary>Scales the factor part of a score: max(0.2, rank / 10). Not capped above 1 (kept as today).</summary>
    public static double For(double rank) => Math.Max(RankFloor, rank / RankMax);
}
