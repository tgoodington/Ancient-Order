using System.Text.Json;
using System.Text.Json.Serialization;

namespace AncientOrder.Rules.Common;

/// <summary>
/// Enums are written as camelCase names and read back only from those exact names: no numbers, no numeric strings,
/// no other casing, no undefined names.
/// </summary>
public sealed class StrictEnumConverter<TEnum> : JsonConverter<TEnum>
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
