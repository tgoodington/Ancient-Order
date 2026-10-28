using AncientOrder.Rules.Common;

namespace AncientOrder.Rules.Tests.Support;

/// <summary>A roll source that always returns the same value, as the TypeScript tests' <c>() =&gt; 15</c> roll functions. Counts its draws.</summary>
public sealed class ConstantRolls : IRollSource
{
    private readonly double _value;

    public ConstantRolls(double value)
    {
        _value = value;
    }

    public int Draws { get; private set; }

    public double Roll()
    {
        Draws++;
        return _value;
    }
}
