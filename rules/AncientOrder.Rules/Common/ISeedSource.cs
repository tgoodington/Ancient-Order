using System.Security.Cryptography;

namespace AncientOrder.Rules.Common;

public interface ISeedSource
{
    ulong NextSeed();
}

public sealed class SystemSeedSource : ISeedSource
{
    public ulong NextSeed() => BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(8), 0);
}
