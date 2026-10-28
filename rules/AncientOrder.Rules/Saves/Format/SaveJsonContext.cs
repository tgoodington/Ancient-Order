using System.Text.Json;
using System.Text.Json.Serialization;

namespace AncientOrder.Rules.Saves.Format;

/// <summary>
/// Enums are written as camelCase names and read back only from those exact names: no numbers, no numeric strings,
/// no other casing, no undefined names.
/// </summary>
public sealed class SaveEnumConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    private static readonly Dictionary<string, TEnum> ByName = Build();

    private static readonly Dictionary<TEnum, string> ByValue = ByName.ToDictionary(p => p.Value, p => p.Key);

    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String && reader.GetString() is { } name && ByName.TryGetValue(name, out var value))
        {
            return value;
        }

        throw new JsonException($"Not a known {typeof(TEnum).Name} name.");
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        if (!ByValue.TryGetValue(value, out var name))
        {
            throw new JsonException($"Not a known {typeof(TEnum).Name} value.");
        }

        writer.WriteStringValue(name);
    }

    private static Dictionary<string, TEnum> Build()
    {
        var map = new Dictionary<string, TEnum>(StringComparer.Ordinal);
        foreach (var value in Enum.GetValues<TEnum>())
        {
            var name = value.ToString();
            map[char.ToLowerInvariant(name[0]) + name[1..]] = value;
        }

        return map;
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    Converters =
    [
        typeof(SaveEnumConverter<Combat.CombatPhase>),
        typeof(SaveEnumConverter<Combat.CombatStatus>),
        typeof(SaveEnumConverter<Combat.ActionType>),
        typeof(SaveEnumConverter<Combat.DefenseType>),
        typeof(SaveEnumConverter<Combat.AscensionLevel>),
        typeof(SaveEnumConverter<Combat.ElementalPath>),
        typeof(SaveEnumConverter<Combat.BuffStat>),
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
            new SaveEnumConverter<Combat.CombatPhase>(),
            new SaveEnumConverter<Combat.CombatStatus>(),
            new SaveEnumConverter<Combat.ActionType>(),
            new SaveEnumConverter<Combat.DefenseType>(),
            new SaveEnumConverter<Combat.AscensionLevel>(),
            new SaveEnumConverter<Combat.ElementalPath>(),
            new SaveEnumConverter<Combat.BuffStat>(),
        },
    };
}
