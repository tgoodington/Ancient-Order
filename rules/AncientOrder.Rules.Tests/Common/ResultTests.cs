using System.Collections.Immutable;
using System.Globalization;
using AncientOrder.Rules.Common;

namespace AncientOrder.Rules.Tests.Common;

public class ResultTests
{
    [Fact]
    public void Ok_carries_its_value()
    {
        var result = Result<int>.Ok(7);

        Assert.True(result.IsOk);
        Assert.Equal(7, result.Value);
        Assert.Throws<InvalidOperationException>(() => result.Error);
    }

    [Fact]
    public void Fail_carries_code_and_message()
    {
        var result = Result<int>.Fail(new RuleError(ErrorCode.NPC_NOT_FOUND, "NPC missing"));

        Assert.False(result.IsOk);
        Assert.Equal(ErrorCode.NPC_NOT_FOUND, result.Error.Code);
        Assert.Equal("NPC missing", result.Error.Message);
        Assert.Empty(result.Error.Details);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Fail_carries_error_details_with_paths()
    {
        var details = ImmutableArray.Create(new RuleErrorDetail("actions/1", "unknown target"));
        var result = Result<string>.Fail(new RuleError(ErrorCode.COMBAT_INVALID_DECLARATION, "bad orders", details));

        var detail = Assert.Single(result.Error.Details);
        Assert.Equal("actions/1", detail.Path);
        Assert.Equal("unknown target", detail.Message);
    }

    [Fact]
    public void Map_transforms_success_and_passes_refusal_through()
    {
        Assert.Equal(8, Result<int>.Ok(4).Map(x => x * 2).Value);

        var error = new RuleError(ErrorCode.VALIDATION_ERROR, "no");
        var refused = Result<int>.Fail(error).Map(x => x * 2);
        Assert.Equal(error, refused.Error);
    }

    [Fact]
    public void Bind_chains_results_and_stops_at_first_refusal()
    {
        var error = new RuleError(ErrorCode.VALIDATION_ERROR, "no");

        Assert.Equal("5", Result<int>.Ok(5).Bind(x => Result<string>.Ok(x.ToString(CultureInfo.InvariantCulture))).Value);
        Assert.Equal(error, Result<int>.Ok(5).Bind(_ => Result<string>.Fail(error)).Error);
        Assert.Equal(error, Result<int>.Fail(error).Bind(_ => Result<string>.Ok("never")).Error);
    }
}
