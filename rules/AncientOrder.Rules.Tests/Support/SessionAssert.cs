using AncientOrder.Rules.Common;

namespace AncientOrder.Rules.Tests.Support;

public static class SessionAssert
{
    /// <summary>Asserts a success and returns its value.</summary>
    public static T Ok<T>(Result<T> result)
    {
        Assert.True(result.IsOk, result.IsOk ? string.Empty : $"{result.Error.Code}: {result.Error.Message}");
        return result.Value;
    }

    /// <summary>Asserts a refusal with the given code and returns its error.</summary>
    public static RuleError Refused<T>(Result<T> result, ErrorCode code)
    {
        Assert.False(result.IsOk, "expected a refusal");
        Assert.Equal(code, result.Error.Code);
        return result.Error;
    }
}
