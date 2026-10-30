using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Content.Packs.Format;
using AncientOrder.Rules.Party;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Content.Packs;

/// <summary>
/// Reads a content pack: pack.json, then each listed file in index order, then builds the game's content from the merged sections.
/// Every problem found comes back together in one refusal; each detail's Path is "file:/json/pointer" (or "file:line L, column C"
/// for a syntax error, or just the file name for a problem with the whole file).
/// </summary>
public static class ContentPackLoader
{
    public const string IndexFileName = "pack.json";
    public const string FormatTag = "ancient-order-content";
    public const int CurrentVersion = 1;

    public static Result<GameContent> Load(IContentSource source) => Load(source, out _);

    /// <summary>Loads like Load and also returns where each merged item came from (empty when the pack did not get as far as merging).</summary>
    public static Result<GameContent> Load(IContentSource source, out ContentProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(source);
        provenance = new ContentProvenance();
        var problems = new List<RuleErrorDetail>();

        var indexText = source.ReadText(IndexFileName);
        if (!indexText.IsOk)
        {
            return Refuse([new RuleErrorDetail(IndexFileName, indexText.Error.Message)]);
        }

        var index = ReadJson(IndexFileName, indexText.Value, ContentJsonContext.Strict.PackIndexFile, problems);
        if (index is null)
        {
            return Refuse(problems);
        }

        CheckIndex(index, problems);

        var files = new List<(string Name, ContentFile Content)>();
        var seenFiles = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < index.Files.Length; i++)
        {
            var name = index.Files[i];
            var pointer = "/files/" + i.ToString(CultureInfo.InvariantCulture);
            if (!IsValidFilePath(name, out var pathProblem))
            {
                problems.Add(new RuleErrorDetail(IndexFileName + ":" + pointer, pathProblem));
                continue;
            }

            if (!seenFiles.Add(name))
            {
                problems.Add(new RuleErrorDetail(IndexFileName + ":" + pointer, $"{name} is listed more than once"));
                continue;
            }

            var text = source.ReadText(name);
            if (!text.IsOk)
            {
                problems.Add(new RuleErrorDetail(name, text.Error.Message));
                continue;
            }

            var content = ReadJson(name, text.Value, ContentJsonContext.Strict.ContentFile, problems);
            if (content is not null)
            {
                files.Add((name, content));
            }
        }

        // Checks across files (settings present, ids, references) would give false alarms about a file that failed to read, so they wait until every file reads.
        if (problems.Count > 0)
        {
            return Refuse(problems);
        }

        var built = Build(index, files, provenance, problems);
        return built is not null && problems.Count == 0 ? Result<GameContent>.Ok(built) : Refuse(problems);
    }

    private static void CheckIndex(PackIndexFile index, List<RuleErrorDetail> problems)
    {
        if (index.Format != FormatTag)
        {
            problems.Add(new RuleErrorDetail(IndexFileName + ":/format", $"format must be \"{FormatTag}\""));
        }

        if (index.Version != CurrentVersion)
        {
            problems.Add(new RuleErrorDetail(
                IndexFileName + ":/version",
                $"unsupported version {index.Version.ToString(CultureInfo.InvariantCulture)}; this game reads version {CurrentVersion.ToString(CultureInfo.InvariantCulture)}"));
        }

        if (string.IsNullOrWhiteSpace(index.Id))
        {
            problems.Add(new RuleErrorDetail(IndexFileName + ":/id", "id must not be empty"));
        }

        if (string.IsNullOrWhiteSpace(index.Name))
        {
            problems.Add(new RuleErrorDetail(IndexFileName + ":/name", "name must not be empty"));
        }

        if (index.Files.IsDefault)
        {
            problems.Add(new RuleErrorDetail(IndexFileName + ":/files", "files must be a list"));
        }
    }

    private static bool IsValidFilePath(string? path, out string problem)
    {
        problem = string.Empty;
        if (string.IsNullOrEmpty(path))
        {
            problem = "file path must not be empty";
        }
        else if (path.Contains('\\', StringComparison.Ordinal))
        {
            problem = $"file path {path} must use forward slashes";
        }
        else if (path.StartsWith('/') || path.Contains(':', StringComparison.Ordinal))
        {
            problem = $"file path {path} must be relative to the pack folder";
        }
        else if (path.Split('/').Any(part => part.Length == 0 || part == "." || part == ".."))
        {
            problem = $"file path {path} must not be empty, '.' or '..' in any part";
        }
        else if (!path.EndsWith(".json", StringComparison.Ordinal))
        {
            problem = $"file path {path} must end in .json";
        }
        else if (path == IndexFileName)
        {
            problem = "pack.json cannot list itself";
        }

        return problem.Length == 0;
    }

    private static GameContent? Build(PackIndexFile index, List<(string Name, ContentFile Content)> files, ContentProvenance provenance, List<RuleErrorDetail> problems)
    {
        var settings = new List<(ContentOrigin Origin, SettingsFormat Item)>();
        var synergy = new List<(ContentOrigin Origin, SynergyFormat Item)>();
        var characters = new List<(ContentOrigin Origin, CharacterFormat Item)>();
        var archetypes = new List<(ContentOrigin Origin, ArchetypeFormat Item)>();
        var fights = new List<(ContentOrigin Origin, FightFormat Item)>();
        var conversations = new List<(ContentOrigin Origin, ConversationFormat Item)>();
        var towns = new List<(ContentOrigin Origin, TownFormat Item)>();

        foreach (var (name, content) in files)
        {
            if (content.Settings is not null)
            {
                settings.Add((new ContentOrigin(name, "/settings"), content.Settings));
            }

            if (content.Synergy is not null)
            {
                synergy.Add((new ContentOrigin(name, "/synergy"), content.Synergy));
            }

            Collect(name, "characters", content.Characters, characters, c => c.Id, "character", problems);
            Collect(name, "archetypes", content.Archetypes, archetypes, a => a.Id, "archetype", problems);
            Collect(name, "fights", content.Fights, fights, f => f.Id, "fight", problems);
            Collect(name, "conversations", content.Conversations, conversations, c => c.Id, "conversation", problems);
            Collect(name, "towns", content.Towns, towns, t => t.Id, "town", problems);
        }

        var firstSettings = OnlyOne(settings, "settings", problems, required: true);
        var firstSynergy = OnlyOne(synergy, "synergy", problems, required: false);

        provenance.Settings = firstSettings?.Origin;
        provenance.Synergy = firstSynergy?.Origin;
        foreach (var c in characters)
        {
            provenance.Characters.Add(c.Origin);
        }

        foreach (var a in archetypes)
        {
            provenance.Archetypes.Add(a.Origin);
        }

        foreach (var f in fights)
        {
            provenance.Fights.Add(f.Origin);
        }

        foreach (var c in conversations)
        {
            provenance.Conversations.Add(c.Origin);
        }

        foreach (var t in towns)
        {
            provenance.Towns.Add(t.Origin);
        }

        // Reference checks need well-formed destinations; when one is malformed, it is reported alone and the reference checks wait for the fix.
        var shapeProblems = problems.Count;
        foreach (var c in conversations)
        {
            CheckShapes(c.Origin, c.Item, problems);
        }

        var townShapeProblems = problems.Count;
        foreach (var t in towns)
        {
            CheckShapes(t.Origin, t.Item, problems);
        }

        var shapesOk = problems.Count == shapeProblems;
        var townShapesOk = problems.Count == townShapeProblems;

        var result = GameContent.Create(
            firstSettings is null ? new GameSettings("placeholder", new LineupLimits(0, 0)) : ContentMapper.ToRules(firstSettings.Value.Item),
            [.. characters.Select(c => ContentMapper.ToRules(c.Item))],
            [.. archetypes.Select(a => ContentMapper.ToRules(a.Item))],
            firstSynergy is null ? null : ContentMapper.ToRules(firstSynergy.Value.Item),
            [.. fights.Select(f => ContentMapper.ToRules(f.Item))],
            new PackInfo(index.Id, index.Name, index.StandIn),
            shapesOk ? [.. conversations.Select(c => ContentMapper.ToRules(c.Item))] : default,
            townShapesOk ? [.. towns.Select(t => ContentMapper.ToRules(t.Item))] : default);

        if (result.IsOk)
        {
            return result.Value;
        }

        foreach (var detail in result.Error.Details)
        {
            if (firstSettings is null && detail.Path.StartsWith("settings", StringComparison.Ordinal))
            {
                continue;
            }

            problems.Add(new RuleErrorDetail(provenance.Translate(detail.Path), detail.Message));
        }

        return null;
    }

    private static void CheckShapes(ContentOrigin origin, ConversationFormat conversation, List<RuleErrorDetail> problems)
    {
        for (var s = 0; s < conversation.Steps.Length; s++)
        {
            var step = conversation.Steps[s];
            var stepPointer = "/steps/" + s.ToString(CultureInfo.InvariantCulture);
            CheckDestinations(origin, stepPointer + "/next", step.Next, problems);
            for (var c = 0; !step.Choices.IsDefault && c < step.Choices.Length; c++)
            {
                CheckDestinations(origin, stepPointer + "/choices/" + c.ToString(CultureInfo.InvariantCulture) + "/next", step.Choices[c].Next, problems);
                CheckShifts(origin, stepPointer + "/choices/" + c.ToString(CultureInfo.InvariantCulture) + "/effects", step.Choices[c].Effects, problems);
            }
        }
    }

    // Points are [x, z] pairs and an area has exactly one of circle and polygon; anything else is reported alone and the town is not built.
    private static void CheckShapes(ContentOrigin origin, TownFormat town, List<RuleErrorDetail> problems)
    {
        CheckPoint(origin, "/start", town.Start, problems);
        for (var g = 0; g < town.Ground.Length; g++)
        {
            CheckPoints(origin, "/ground/" + Num(g), town.Ground[g], problems);
        }

        for (var o = 0; !town.Obstacles.IsDefault && o < town.Obstacles.Length; o++)
        {
            CheckPoints(origin, "/obstacles/" + Num(o) + "/footprint", town.Obstacles[o].Footprint, problems);
        }

        for (var p = 0; !town.People.IsDefault && p < town.People.Length; p++)
        {
            for (var l = 0; l < town.People[p].Placements.Length; l++)
            {
                CheckPoint(origin, "/people/" + Num(p) + "/placements/" + Num(l) + "/at", town.People[p].Placements[l].At, problems);
            }

            CheckOpens(origin, "/people/" + Num(p) + "/opens", town.People[p].Opens, problems);
        }

        for (var s = 0; !town.Spots.IsDefault && s < town.Spots.Length; s++)
        {
            var area = town.Spots[s].Area;
            CheckOpens(origin, "/spots/" + Num(s) + "/opens", town.Spots[s].Opens, problems);
            var areaPointer = "/spots/" + Num(s) + "/area";
            if ((area.Circle is null) == area.Polygon.IsDefault)
            {
                problems.Add(new RuleErrorDetail(origin.Location(areaPointer), "an area needs exactly one of circle or polygon"));
            }
            else if (area.Circle is { } circle)
            {
                CheckPoint(origin, areaPointer + "/circle/center", circle.Center, problems);
            }
            else
            {
                CheckPoints(origin, areaPointer + "/polygon", area.Polygon, problems);
            }
        }
    }

    private static void CheckOpens(ContentOrigin origin, string pointer, ImmutableArray<OpensFormat> opens, List<RuleErrorDetail> problems)
    {
        for (var i = 0; !opens.IsDefault && i < opens.Length; i++)
        {
            if ((opens[i].Conversation is null) == (opens[i].HandOff is null))
            {
                problems.Add(new RuleErrorDetail(origin.Location(pointer + "/" + Num(i)), "an opens entry needs exactly one of conversation or handOff"));
            }
        }
    }

    private static void CheckPoints(ContentOrigin origin, string pointer, ImmutableArray<ImmutableArray<double>> points, List<RuleErrorDetail> problems)
    {
        for (var i = 0; i < points.Length; i++)
        {
            CheckPoint(origin, pointer + "/" + Num(i), points[i], problems);
        }
    }

    private static void CheckPoint(ContentOrigin origin, string pointer, ImmutableArray<double> point, List<RuleErrorDetail> problems)
    {
        if (point.IsDefault || point.Length != 2)
        {
            problems.Add(new RuleErrorDetail(origin.Location(pointer), "a point must be [x, z]"));
        }
    }

    private static string Num(int i) => i.ToString(CultureInfo.InvariantCulture);

    private static void CheckShifts(ContentOrigin origin, string pointer, ImmutableArray<EffectFormat> effects, List<RuleErrorDetail> problems)
    {
        for (var e = 0; !effects.IsDefault && e < effects.Length; e++)
        {
            if (effects[e] is ShiftEffectFormat shift
                && new[] { shift.Patience, shift.Empathy, shift.Cunning, shift.Logic, shift.Kindness, shift.Charisma }.Any(v => v is { } n && !double.IsFinite(n)))
            {
                problems.Add(new RuleErrorDetail(origin.Location(pointer + "/" + e.ToString(CultureInfo.InvariantCulture)), "a shift must be a finite number"));
            }
        }
    }

    private static void CheckDestinations(ContentOrigin origin, string pointer, ImmutableArray<DestinationFormat> next, List<RuleErrorDetail> problems)
    {
        for (var n = 0; !next.IsDefault && n < next.Length; n++)
        {
            if ((next[n].Step is null) == (next[n].Conversation is null))
            {
                problems.Add(new RuleErrorDetail(
                    origin.Location(pointer + "/" + n.ToString(CultureInfo.InvariantCulture)),
                    "a destination needs exactly one of step or conversation"));
            }
        }
    }

    private static void Collect<T>(
        string file,
        string section,
        ImmutableArray<T> items,
        List<(ContentOrigin Origin, T Item)> into,
        Func<T, string> idOf,
        string noun,
        List<RuleErrorDetail> problems)
        where T : class
    {
        if (items.IsDefault)
        {
            return;
        }

        for (var i = 0; i < items.Length; i++)
        {
            var origin = new ContentOrigin(file, "/" + section + "/" + i.ToString(CultureInfo.InvariantCulture));
            var item = items[i];
            if (item is null)
            {
                problems.Add(new RuleErrorDetail(origin.Location(), $"{noun} must not be null"));
                continue;
            }

            var id = idOf(item);
            var first = into.FindIndex(e => idOf(e.Item) == id);
            if (first >= 0)
            {
                problems.Add(new RuleErrorDetail(origin.Location("/id"), $"duplicate {noun} id: {id} (first defined at {into[first].Origin.Location("/id")})"));
                continue;
            }

            into.Add((origin, item));
        }
    }

    private static (ContentOrigin Origin, T Item)? OnlyOne<T>(List<(ContentOrigin Origin, T Item)> found, string section, List<RuleErrorDetail> problems, bool required)
    {
        if (found.Count == 0)
        {
            if (required)
            {
                problems.Add(new RuleErrorDetail(IndexFileName + ":/files", $"no file has a {section} section; exactly one is needed"));
            }

            return null;
        }

        for (var i = 1; i < found.Count; i++)
        {
            problems.Add(new RuleErrorDetail(found[i].Origin.Location(), $"more than one {section} section; the first is at {found[0].Origin.Location()}"));
        }

        return found[0];
    }

    private static T? ReadJson<T>(string file, string text, JsonTypeInfo<T> typeInfo, List<RuleErrorDetail> problems)
        where T : class
    {
        try
        {
            using var document = JsonDocument.Parse(text, ContentJsonContext.DocumentOptions);
            // System.Text.Json does not apply nullable annotations to list elements, so a null element would reach the checks and mapping as null.
            var nulls = problems.Count;
            FindNullItems(file, document.RootElement, string.Empty, problems);
            if (problems.Count > nulls)
            {
                return null;
            }
        }
        catch (JsonException ex)
        {
            var place = ex.LineNumber is { } line && ex.BytePositionInLine is { } column
                ? $"line {(line + 1).ToString(CultureInfo.InvariantCulture)}, column {(column + 1).ToString(CultureInfo.InvariantCulture)}"
                : FindDuplicatePlace(text);
            problems.Add(new RuleErrorDetail(place.Length == 0 ? file : file + ":" + place, Clean(ex.Message)));
            return null;
        }

        try
        {
            var value = JsonSerializer.Deserialize(text, typeInfo);
            if (value is null)
            {
                problems.Add(new RuleErrorDetail(file, "the file must be a JSON object"));
            }

            return value;
        }
        catch (JsonException ex)
        {
            var pointer = ex.Path is null ? string.Empty : ToPointer(ex.Path);
            problems.Add(new RuleErrorDetail(pointer.Length == 0 ? file : file + ":" + pointer, Clean(ex.Message)));
            return null;
        }
        catch (NotSupportedException ex)
        {
            // A missing or unknown "type" on an effect is thrown as NotSupportedException, with the place only in the message.
            var marker = " Path: ";
            var at = ex.Message.IndexOf(marker, StringComparison.Ordinal);
            var end = at < 0 ? -1 : ex.Message.IndexOf(" |", at, StringComparison.Ordinal);
            var pointer = at < 0 ? string.Empty : ToPointer(ex.Message[(at + marker.Length)..(end < 0 ? ex.Message.Length : end)].Trim());
            problems.Add(new RuleErrorDetail(pointer.Length == 0 ? file : file + ":" + pointer, Clean(ex.Message)));
            return null;
        }
    }

    private static string FindDuplicatePlace(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var reader = new Utf8JsonReader(bytes);
        var scopes = new Stack<HashSet<string>?>();
        try
        {
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.StartObject:
                        scopes.Push(new HashSet<string>(StringComparer.Ordinal));
                        break;
                    case JsonTokenType.StartArray:
                        scopes.Push(null);
                        break;
                    case JsonTokenType.EndObject:
                    case JsonTokenType.EndArray:
                        scopes.Pop();
                        break;
                    case JsonTokenType.PropertyName:
                        if (!scopes.Peek()!.Add(reader.GetString()!))
                        {
                            var start = (int)reader.TokenStartIndex;
                            var line = 1;
                            var lineStart = 0;
                            for (var i = 0; i < start; i++)
                            {
                                if (bytes[i] == (byte)'\n')
                                {
                                    line++;
                                    lineStart = i + 1;
                                }
                            }

                            return $"line {line.ToString(CultureInfo.InvariantCulture)}, column {(start - lineStart + 1).ToString(CultureInfo.InvariantCulture)}";
                        }

                        break;
                    default:
                        break;
                }
            }
        }
        catch (JsonException)
        {
        }

        return string.Empty;
    }

    private static void FindNullItems(string file, JsonElement element, string pointer, List<RuleErrorDetail> problems)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            var i = 0;
            foreach (var item in element.EnumerateArray())
            {
                var itemPointer = pointer + "/" + i.ToString(CultureInfo.InvariantCulture);
                if (item.ValueKind == JsonValueKind.Null)
                {
                    problems.Add(new RuleErrorDetail(file + ":" + itemPointer, "item is null"));
                }
                else
                {
                    FindNullItems(file, item, itemPointer, problems);
                }

                i++;
            }
        }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                FindNullItems(file, property.Value, pointer + "/" + property.Name.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal), problems);
            }
        }
    }

    private static string Clean(string message)
    {
        var cut = message.Length;
        foreach (var marker in new[] { " Path: ", " LineNumber: " })
        {
            var at = message.IndexOf(marker, StringComparison.Ordinal);
            if (at >= 0 && at < cut)
            {
                cut = at;
            }
        }

        return message[..cut].Trim();
    }

    /// <summary>Turns System.Text.Json's "$.characters[0].archetypeId" into the pointer "/characters/0/archetypeId". The root, "$", has no pointer.</summary>
    internal static string ToPointer(string jsonPath)
    {
        var pointer = new StringBuilder();
        var i = jsonPath.StartsWith('$') ? 1 : 0;
        while (i < jsonPath.Length)
        {
            if (jsonPath[i] == '.')
            {
                var start = ++i;
                while (i < jsonPath.Length && jsonPath[i] != '.' && jsonPath[i] != '[')
                {
                    i++;
                }

                AppendSegment(pointer, jsonPath[start..i]);
            }
            else if (jsonPath[i] == '[')
            {
                var start = ++i;
                if (i < jsonPath.Length && jsonPath[i] == '\'')
                {
                    start = ++i;
                    var end = jsonPath.IndexOf("']", i, StringComparison.Ordinal);
                    i = end < 0 ? jsonPath.Length : end;
                    AppendSegment(pointer, jsonPath[start..i]);
                    i = Math.Min(i + 2, jsonPath.Length);
                }
                else
                {
                    var end = jsonPath.IndexOf(']', i);
                    i = end < 0 ? jsonPath.Length : end;
                    AppendSegment(pointer, jsonPath[start..i]);
                    i = Math.Min(i + 1, jsonPath.Length);
                }
            }
            else
            {
                i++;
            }
        }

        return pointer.ToString();
    }

    private static void AppendSegment(StringBuilder pointer, string segment) =>
        pointer.Append('/').Append(segment.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal));

    private static Result<GameContent> Refuse(List<RuleErrorDetail> problems)
    {
        var more = problems.Count > 1
            ? $" (and {(problems.Count - 1).ToString(CultureInfo.InvariantCulture)} more)"
            : string.Empty;
        return Result<GameContent>.Fail(new RuleError(
            ErrorCode.VALIDATION_ERROR,
            "Content pack is invalid: " + problems[0].Message + more,
            [.. problems]));
    }
}
