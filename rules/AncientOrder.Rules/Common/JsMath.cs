namespace AncientOrder.Rules.Common;

public static class JsMath
{
    /// <summary>JavaScript Math.round: halves round toward +infinity.</summary>
    public static double Round(double x)
    {
        var f = Math.Floor(x);
        return x - f >= 0.5 ? f + 1 : f;
    }
}
