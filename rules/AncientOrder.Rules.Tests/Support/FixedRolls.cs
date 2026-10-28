using AncientOrder.Rules.Common;

namespace AncientOrder.Rules.Tests.Support;

public sealed class FixedRolls : IRollSource
{
    private readonly double[] _rolls;
    private int _next;

    public FixedRolls(params double[] rolls)
    {
        _rolls = rolls;
    }

    public int Consumed => _next;

    public double Roll()
    {
        if (_next >= _rolls.Length)
        {
            Assert.Fail($"FixedRolls ran out: {_rolls.Length} roll(s) scripted, roll {_next + 1} requested.");
        }

        return _rolls[_next++];
    }
}
