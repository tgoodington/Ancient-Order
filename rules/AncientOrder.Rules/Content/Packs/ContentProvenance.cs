using System.Globalization;
using System.Text.Json;

namespace AncientOrder.Rules.Content.Packs;

/// <summary>Where one merged item came from: the content file and the JSON pointer of the item in it.</summary>
public sealed record ContentOrigin(string File, string JsonPointer)
{
    public string Location(string extraPointer = "") => File + ":" + JsonPointer + extraPointer;
}

/// <summary>
/// Remembers the origin of every merged item, in merged order, so a problem GameContent.Create reports at
/// "characters/3/archetypeId" can be told as "cast.json:/characters/0/archetypeId". The path segments after the item
/// are the rules' own names, which equal the format's names once put in camelCase.
/// </summary>
public sealed class ContentProvenance
{
    public ContentOrigin? Settings { get; set; }

    public ContentOrigin? Synergy { get; set; }

    public List<ContentOrigin> Characters { get; } = [];

    public List<ContentOrigin> Archetypes { get; } = [];

    public List<ContentOrigin> Fights { get; } = [];

    public List<ContentOrigin> Conversations { get; } = [];

    public List<ContentOrigin> Towns { get; } = [];

    /// <summary>Turns a GameContent.Create detail path into "file:pointer". A path it cannot place is returned unchanged.</summary>
    public string Translate(string rulesPath)
    {
        var parts = rulesPath.Split('/');
        ContentOrigin? origin;
        var used = 1;
        switch (parts[0])
        {
            case "settings":
                origin = Settings;
                break;
            case "synergy":
                origin = Synergy;
                break;
            case "characters":
                origin = Item(Characters, parts);
                used = 2;
                break;
            case "archetypes":
                origin = Item(Archetypes, parts);
                used = 2;
                break;
            case "encounters":
                origin = Item(Fights, parts);
                used = 2;
                break;
            case "conversations":
                origin = Item(Conversations, parts);
                used = 2;
                break;
            case "towns":
                origin = Item(Towns, parts);
                used = 2;
                break;
            default:
                return rulesPath;
        }

        if (origin is null)
        {
            return rulesPath;
        }

        var rest = new System.Text.StringBuilder();
        for (var i = used; i < parts.Length; i++)
        {
            rest.Append('/').Append(int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out _)
                ? parts[i]
                : JsonNamingPolicy.CamelCase.ConvertName(parts[i]));
        }

        return origin.Location(rest.ToString());
    }

    private static ContentOrigin? Item(List<ContentOrigin> list, string[] parts) =>
        parts.Length >= 2 && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var index) && index < list.Count
            ? list[index]
            : null;
}
