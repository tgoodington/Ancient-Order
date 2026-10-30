using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json.Nodes;

namespace AncientOrder.Rules.Saves;

/// <summary>One step from FromVersion to FromVersion + 1, working on the JSON object before it is mapped.</summary>
public sealed record SaveUpgradeStep(int FromVersion, Action<JsonObject> Apply);

public static class SaveUpgrades
{
    /// <summary>One step per version. Add a step here whenever CurrentVersion goes up.</summary>
    public static ImmutableArray<SaveUpgradeStep> Steps { get; } = [new SaveUpgradeStep(1, OneToTwo)];

    /// <summary>
    /// Version 2 adds the party, town, conversation, story memory, hand-off, and the envelope summary. A version 1 save's party is its
    /// lineup; the rest start empty (the reconciler places the player in the starting town).
    /// </summary>
    private static void OneToTwo(JsonObject save)
    {
        var game = save["game"]?.AsObject() ?? throw new InvalidOperationException("game missing");
        game["party"] = game["lineup"]?.DeepClone() ?? new JsonArray();
        game["town"] = null;
        game["conversation"] = null;
        game["memory"] = new JsonObject
        {
            ["flags"] = new JsonArray(),
            ["chosen"] = new JsonArray(),
            ["seen"] = new JsonArray(),
        };
        game["handOff"] = null;
        save["summary"] = new JsonObject { ["situation"] = "none" };
    }

    /// <summary>Applies steps in sequence until the save reaches targetVersion. Returns null on success, or the reason it could not.</summary>
    public static string? Run(JsonObject save, long version, int targetVersion, ImmutableArray<SaveUpgradeStep> steps)
    {
        var current = version;
        while (current < targetVersion)
        {
            var step = steps.IsDefault ? null : steps.FirstOrDefault(s => s.FromVersion == current);
            if (step is null)
            {
                return "unsupported version " + current.ToString(CultureInfo.InvariantCulture);
            }

            try
            {
                step.Apply(save);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or FormatException or InvalidCastException or KeyNotFoundException)
            {
                return "upgrade from version " + current.ToString(CultureInfo.InvariantCulture) + " failed";
            }

            current++;
            save["version"] = current;
        }

        return null;
    }
}
