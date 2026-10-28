using System.Text.Json;
using System.Text.Json.Nodes;

namespace AncientOrder.Rules.Tests.Support;

public static class StateAssert
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    /// <summary>Compares two states by JSON form; object property order is ignored, array order is not.</summary>
    public static void Equivalent<T>(T expected, T actual)
    {
        var expectedNode = JsonSerializer.SerializeToNode(expected);
        var actualNode = JsonSerializer.SerializeToNode(actual);
        if (!JsonNode.DeepEquals(expectedNode, actualNode))
        {
            Assert.Fail(
                "States are not equivalent.\nExpected:\n"
                + (expectedNode?.ToJsonString(Indented) ?? "null")
                + "\nActual:\n"
                + (actualNode?.ToJsonString(Indented) ?? "null"));
        }
    }
}
