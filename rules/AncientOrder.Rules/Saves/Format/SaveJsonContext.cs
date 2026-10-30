using System.Text.Json;
using System.Text.Json.Serialization;
using AncientOrder.Rules.Common;

namespace AncientOrder.Rules.Saves.Format;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    Converters =
    [
        typeof(StrictEnumConverter<Combat.CombatPhase>),
        typeof(StrictEnumConverter<Combat.CombatStatus>),
        typeof(StrictEnumConverter<Combat.ActionType>),
        typeof(StrictEnumConverter<Combat.DefenseType>),
        typeof(StrictEnumConverter<Combat.AscensionLevel>),
        typeof(StrictEnumConverter<Combat.ElementalPath>),
        typeof(StrictEnumConverter<Combat.BuffStat>),
        typeof(StrictEnumConverter<SaveSituation>),
    ])]
[JsonSerializable(typeof(SaveEnvelope))]
public sealed partial class SaveJsonContext : JsonSerializerContext
{
    /// <summary>
    /// The context saves are written and read with. The attribute above only configures Default; a context made with
    /// its own options needs them spelled out, and these add the fixed newline and the nesting limit.
    /// </summary>
    public static SaveJsonContext Strict { get; } = new(CreateOptions());

    private static JsonSerializerOptions CreateOptions() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        NewLine = "\n",
        MaxDepth = SaveConstants.MaxJsonDepth,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        Converters =
        {
            new StrictEnumConverter<Combat.CombatPhase>(),
            new StrictEnumConverter<Combat.CombatStatus>(),
            new StrictEnumConverter<Combat.ActionType>(),
            new StrictEnumConverter<Combat.DefenseType>(),
            new StrictEnumConverter<Combat.AscensionLevel>(),
            new StrictEnumConverter<Combat.ElementalPath>(),
            new StrictEnumConverter<Combat.BuffStat>(),
            new StrictEnumConverter<SaveSituation>(),
        },
    };
}
