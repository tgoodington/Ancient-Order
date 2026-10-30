namespace AncientOrder.Tests;

/// <summary>Thrown by Assert; the runner reports its message as the test's failure.</summary>
public sealed class TestFailure(string message) : Exception(message)
{
}

/// <summary>A minimal assertion set. Every failure throws TestFailure. Equal takes the expected value first.</summary>
public static class Assert
{
    public static void Fail(string message) => throw new TestFailure(message);

    public static void True(bool condition, string message)
    {
        if (!condition)
        {
            Fail(message);
        }
    }

    public static void False(bool condition, string message) => True(!condition, message);

    public static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            Fail($"{message} (expected {expected?.ToString() ?? "null"}, got {actual?.ToString() ?? "null"})");
        }
    }

    public static T NotNull<T>(T? value, string message)
        where T : class
    {
        if (value is null)
        {
            Fail(message);
        }

        return value!;
    }

    public static void Null(object? value, string message)
    {
        if (value is not null)
        {
            Fail($"{message} (got {value})");
        }
    }

    /// <summary>Passes when the text holds the expected piece (ordinal, case sensitive).</summary>
    public static void Contains(string expected, string? text, string message)
    {
        if (text is null || !text.Contains(expected, StringComparison.Ordinal))
        {
            Fail($"{message} (expected \"{expected}\" in \"{text}\")");
        }
    }

    /// <summary>The no-numbers guard: passes when the text has no digit.</summary>
    public static void NoDigits(string? text, string message)
    {
        if (text is not null && text.Any(char.IsDigit))
        {
            Fail($"{message} (digits in \"{text}\")");
        }
    }
}
