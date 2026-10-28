namespace AncientOrder.Rules.Common;

public sealed record DiceState(ulong S0, ulong S1, ulong S2, ulong S3)
{
    public static DiceState FromSeed(ulong seed)
    {
        var x = seed;
        var s0 = SplitMix64(ref x);
        var s1 = SplitMix64(ref x);
        var s2 = SplitMix64(ref x);
        var s3 = SplitMix64(ref x);
        return new DiceState(s0, s1, s2, s3);
    }

    private static ulong SplitMix64(ref ulong x)
    {
        unchecked
        {
            x += 0x9E3779B97F4A7C15UL;
            var z = x;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}

/// <summary>xoshiro256** seeded through SplitMix64. Integer-only state, so its sequence never depends on the .NET version.</summary>
public sealed class SeededRolls : IRollSource
{
    private ulong _s0;
    private ulong _s1;
    private ulong _s2;
    private ulong _s3;

    private SeededRolls(DiceState state)
    {
        if ((state.S0 | state.S1 | state.S2 | state.S3) == 0)
        {
            throw new ArgumentException("The all-zero dice state is invalid.", nameof(state));
        }

        (_s0, _s1, _s2, _s3) = (state.S0, state.S1, state.S2, state.S3);
    }

    public DiceState State => new(_s0, _s1, _s2, _s3);

    public static SeededRolls FromSeed(ulong seed) => new(DiceState.FromSeed(seed));

    public static SeededRolls FromState(DiceState state) => new(state);

    public double Roll() => (Next() >> 11) * (1.0 / 9007199254740992.0) * 20;

    private ulong Next()
    {
        unchecked
        {
            var result = RotL(_s1 * 5, 7) * 9;
            var t = _s1 << 17;
            _s2 ^= _s0;
            _s3 ^= _s1;
            _s1 ^= _s2;
            _s0 ^= _s3;
            _s2 ^= t;
            _s3 = RotL(_s3, 45);
            return result;
        }
    }

    private static ulong RotL(ulong x, int k) => (x << k) | (x >> (64 - k));
}
