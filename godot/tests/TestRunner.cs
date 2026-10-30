using System.Reflection;
using System.Runtime.ExceptionServices;
using Godot;

namespace AncientOrder.Tests;

/// <summary>
/// The in-engine screen test runner. On _Ready it finds every public [GodotTest] method in the classes of the AncientOrder.Tests namespace, runs them one at a time
/// in "Class.Method" name order (each on a fresh instance with a fresh TestContext, its Root freed afterwards), prints "PASS Class.Method" or "FAIL Class.Method: message"
/// per test and a summary line, and quits with exit code 1 on any failure (or when nothing ran) and 0 otherwise.
/// Run: godot --headless --path godot res://tests/TestRunner.tscn. Narrow it with user arguments after "--": "-- --filter=text" runs the tests whose "Class.Method" holds the text.
/// To add a test, put a class with [GodotTest] methods anywhere under godot/tests in namespace AncientOrder.Tests; nothing else to register.
/// </summary>
public partial class TestRunner : Node
{
    private const string TestNamespace = "AncientOrder.Tests";
    private const string FilterPrefix = "--filter=";

    public override async void _Ready()
    {
        var code = 1;
        try
        {
            code = await RunAll();
        }
        catch (Exception ex)
        {
            GD.PrintErr("Godot tests: the runner itself failed: " + ex);
        }

        GetTree().Quit(code);
    }

    private static string? Filter() =>
        OS.GetCmdlineUserArgs().Where(a => a.StartsWith(FilterPrefix, StringComparison.Ordinal)).Select(a => a[FilterPrefix.Length..]).FirstOrDefault();

    private static List<(string Name, Type Type, MethodInfo Method, int Timeout)> Discover(string? filter)
    {
        var found = new List<(string Name, Type Type, MethodInfo Method, int Timeout)>();
        foreach (var type in typeof(TestRunner).Assembly.GetTypes())
        {
            var space = type.Namespace;
            if (!type.IsClass || type.IsAbstract || space is null || !(space == TestNamespace || space.StartsWith(TestNamespace + ".", StringComparison.Ordinal)))
            {
                continue;
            }

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (method.GetCustomAttribute<GodotTestAttribute>() is { } attribute)
                {
                    var name = type.Name + "." + method.Name;
                    if (filter is null || name.Contains(filter, StringComparison.Ordinal))
                    {
                        found.Add((name, type, method, attribute.TimeoutSeconds));
                    }
                }
            }
        }

        found.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        return found;
    }

    private async Task<int> RunAll()
    {
        var filter = Filter();
        var tests = Discover(filter);
        var failed = 0;
        foreach (var (name, type, method, timeout) in tests)
        {
            var failure = await RunOne(type, method, timeout);
            if (failure is null)
            {
                GD.Print("PASS " + name);
            }
            else
            {
                failed++;
                GD.Print($"FAIL {name}: {failure}");
            }
        }

        if (tests.Count == 0)
        {
            GD.Print(filter is null ? "Godot tests: FAILED, no tests found" : $"Godot tests: FAILED, no test matches \"{filter}\"");
            return 1;
        }

        GD.Print($"Godot tests: {tests.Count - failed} passed, {failed} failed, {tests.Count} total");
        return failed == 0 ? 0 : 1;
    }

    private async Task<string?> RunOne(Type type, MethodInfo method, int timeoutSeconds)
    {
        var root = new Node { Name = "TestRoot" };
        AddChild(root);
        var context = new TestContext(GetTree(), root);
        try
        {
            var invoking = Invoke(type, method, context);
            var timedOut = new TaskCompletionSource();
            GetTree().CreateTimer(timeoutSeconds).Timeout += () => timedOut.TrySetResult();
            if (await Task.WhenAny(invoking, timedOut.Task) != invoking)
            {
                _ = invoking.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
                return $"timed out after {timeoutSeconds} s";
            }

            await invoking;
            return null;
        }
        catch (TestFailure failure)
        {
            return failure.Message;
        }
        catch (Exception ex)
        {
            return $"{ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            root.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private static async Task Invoke(Type type, MethodInfo method, TestContext context)
    {
        if (method.ReturnType != typeof(void) && method.ReturnType != typeof(Task))
        {
            throw new InvalidOperationException("a test must return void or Task");
        }

        var parameters = method.GetParameters();
        object?[] arguments = parameters.Length switch
        {
            0 => [],
            1 when parameters[0].ParameterType == typeof(TestContext) => [context],
            _ => throw new InvalidOperationException("a test takes no arguments or one TestContext"),
        };

        var instance = Activator.CreateInstance(type)!;
        try
        {
            if (method.Invoke(instance, arguments) is Task task)
            {
                await task;
            }
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
        }
    }
}
