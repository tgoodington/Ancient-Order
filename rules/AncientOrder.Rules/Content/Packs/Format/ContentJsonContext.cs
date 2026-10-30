using System.Text.Json;
using System.Text.Json.Serialization;
using AncientOrder.Rules.Common;

namespace AncientOrder.Rules.Content.Packs.Format;

/// <summary>
/// Reads content files strictly: camelCase names, no unknown or duplicate properties, every required property present,
/// no nulls for non-nullable values, and enums only as exact camelCase names. Trailing commas and comments are tolerated.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    AllowTrailingCommas = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    Converters =
    [
        typeof(StrictEnumConverter<Combat.ElementalPath>),
        typeof(StrictEnumConverter<Combat.AscensionLevel>),
        typeof(StrictEnumConverter<Combat.CombatStat>),
        typeof(StrictEnumConverter<Combat.StatStep>),
        typeof(StrictEnumConverter<Party.SynergyRule>),
        typeof(StrictEnumConverter<Party.SynergyResolution>),
        typeof(StrictEnumConverter<Personality.Trait>),
        typeof(StrictEnumConverter<Conditions.RelationshipMeasure>),
        typeof(StrictEnumConverter<Town.SpotTrigger>),
    ])]
[JsonSerializable(typeof(PackIndexFile))]
[JsonSerializable(typeof(ContentFile))]
public sealed partial class ContentJsonContext : JsonSerializerContext
{
    /// <summary>The context content is read with. The attribute above only configures Default, so these options spell the same rules out and add the duplicate-property refusal.</summary>
    public static ContentJsonContext Strict { get; } = new(CreateOptions());

    /// <summary>The same rules for the syntax pre-check, which finds problems with a line and column before the typed read.</summary>
    public static JsonDocumentOptions DocumentOptions { get; } = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
        AllowDuplicateProperties = false,
    };

    private static JsonSerializerOptions CreateOptions() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        Converters =
        {
            new StrictEnumConverter<Combat.ElementalPath>(),
            new StrictEnumConverter<Combat.AscensionLevel>(),
            new StrictEnumConverter<Combat.CombatStat>(),
            new StrictEnumConverter<Combat.StatStep>(),
            new StrictEnumConverter<Party.SynergyRule>(),
            new StrictEnumConverter<Party.SynergyResolution>(),
            new StrictEnumConverter<Personality.Trait>(),
            new StrictEnumConverter<Conditions.RelationshipMeasure>(),
            new StrictEnumConverter<Town.SpotTrigger>(),
        },
    };
}
