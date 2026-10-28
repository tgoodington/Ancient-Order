namespace AncientOrder.Rules.Common;

public interface IIdSource
{
    string NextId();
}

public sealed class GuidIdSource : IIdSource
{
    public string NextId() => Guid.NewGuid().ToString("D");
}
