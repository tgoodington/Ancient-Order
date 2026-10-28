using System.Diagnostics.CodeAnalysis;

namespace AncientOrder.Rules.Common;

[SuppressMessage("Design", "CA1000", Justification = "Result<T>.Ok and Result<T>.Fail are the specified call shape.")]
public sealed record Result<T>
{
    private readonly T? _value;
    private readonly RuleError? _error;

    private Result(T? value, RuleError? error)
    {
        _value = value;
        _error = error;
    }

    public bool IsOk => _error is null;

    public T Value => _error is null
        ? _value!
        : throw new InvalidOperationException("Result is a refusal and has no value.");

    public RuleError Error => _error
        ?? throw new InvalidOperationException("Result is a success and has no error.");

    public static Result<T> Ok(T value) => new(value, null);

    public static Result<T> Fail(RuleError error) => new(default, error);

    public Result<TOut> Map<TOut>(Func<T, TOut> map) =>
        IsOk ? Result<TOut>.Ok(map(Value)) : Result<TOut>.Fail(Error);

    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> bind) =>
        IsOk ? bind(Value) : Result<TOut>.Fail(Error);
}
