namespace AncientOrder.Rules.Common;

public interface IRollSource
{
    /// <summary>Returns a value in [0, 20), like the TypeScript rollFn (Math.random() * 20).</summary>
    double Roll();
}
