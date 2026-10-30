namespace AncientOrder.Tests;

/// <summary>
/// Marks a public method of a class in the AncientOrder.Tests namespace as a screen test. The method returns void or Task and takes no
/// arguments or one TestContext. The runner makes a fresh instance of the class (it needs a public parameterless constructor) for every test.
/// TimeoutSeconds is how long the test may run before it fails.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class GodotTestAttribute : Attribute
{
    public int TimeoutSeconds { get; set; } = 10;
}
