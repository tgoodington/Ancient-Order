using AncientOrder.Rules.Common;

namespace AncientOrder.Rules.Tests.Architecture;

public class RulesIndependenceTests
{
    [Fact]
    public void Rules_assembly_references_only_framework_assemblies()
    {
        var references = typeof(Result<>).Assembly.GetReferencedAssemblies();
        Assert.NotEmpty(references);

        foreach (var reference in references)
        {
            var name = reference.Name ?? string.Empty;
            var isFramework = name is "System" or "mscorlib" or "netstandard"
                || name.StartsWith("System.", StringComparison.Ordinal);
            Assert.True(isFramework, $"AncientOrder.Rules references non-framework assembly '{name}'. The rules library must stay dependency-free.");
            Assert.False(name.StartsWith("Godot", StringComparison.OrdinalIgnoreCase), $"AncientOrder.Rules references '{name}'.");
        }
    }
}
