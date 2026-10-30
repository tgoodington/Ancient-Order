using AncientOrder.Rules.Common;
using AncientOrder.Rules.Content.Packs;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Tests.PackIntake;

public sealed class PackLoaderTests : IDisposable
{
    private const string Index = """{ "format": "ancient-order-content", "version": 1, "id": "t", "name": "Test", "standIn": false, "files": ["a.json", "b.json"] }""";

    private const string Settings = """{ "settings": { "playerName": "P", "lineup": { "min": 1, "max": 1 } } }""";

    private const string Character = """{ "id": "ann", "name": "Ann", "archetypeId": "x", "personality": { "patience": 20, "empathy": 20, "cunning": 10, "logic": 15, "kindness": 20, "charisma": 15 }, "startingAffection": 0, "startingTrust": 0 }""";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "ao-loader-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static MemoryContentSource Memory(string a, string b = "{}") =>
        new([KeyValuePair.Create("pack.json", Index), KeyValuePair.Create("a.json", a), KeyValuePair.Create("b.json", b)]);

    private static string[] Paths(Result<GameContent> result)
    {
        Assert.False(result.IsOk);
        Assert.Equal(ErrorCode.VALIDATION_ERROR, result.Error.Code);
        return [.. result.Error.Details.Select(d => d.Path)];
    }

    [Fact]
    public void A_pack_in_a_folder_loads_the_same_as_the_same_pack_in_memory()
    {
        Directory.CreateDirectory(_root);
        var a = Settings;
        var b = $$"""{ "archetypes": [{ "id": "x" }], "characters": [ {{Character}} ] }""";
        File.WriteAllText(Path.Combine(_root, "pack.json"), Index);
        File.WriteAllText(Path.Combine(_root, "a.json"), a);
        File.WriteAllText(Path.Combine(_root, "b.json"), b);

        var fromFolder = ContentPackLoader.Load(new FolderContentSource(_root));
        var fromMemory = ContentPackLoader.Load(Memory(a, b));

        Assert.True(fromFolder.IsOk, fromFolder.IsOk ? string.Empty : string.Join("\n", fromFolder.Error.Details));
        Assert.True(fromMemory.IsOk);
        Assert.Equal(new PackInfo("t", "Test", false), fromFolder.Value.Pack);
        Assert.Equal(fromMemory.Value.Settings, fromFolder.Value.Settings);
        Assert.Equal(fromMemory.Value.Characters.Definitions, fromFolder.Value.Characters.Definitions);
        Assert.True(fromFolder.Value.Characters.TryGet("ann", out var ann));
        Assert.Equal("Ann", ann.Name);
    }

    [Fact]
    public void Files_merge_in_index_order_so_items_keep_the_order_of_the_files_listing_them()
    {
        var result = ContentPackLoader.Load(Memory(
            """{ "settings": { "playerName": "P", "lineup": { "min": 1, "max": 1 } }, "archetypes": [{ "id": "first" }, { "id": "second" }] }""",
            """{ "archetypes": [{ "id": "third" }] }"""));

        Assert.True(result.IsOk);
        Assert.Equal(["first", "second", "third"], result.Value.Archetypes.Definitions.Select(d => d.Id));
    }

    [Fact]
    public void A_reference_to_nothing_is_told_as_the_file_it_was_written_in()
    {
        var result = ContentPackLoader.Load(
            Memory(Settings, """{ "archetypes": [{ "id": "x" }], "characters": [ { "id": "ann", "name": "Ann", "archetypeId": "gone", "personality": { "patience": 20, "empathy": 20, "cunning": 10, "logic": 15, "kindness": 20, "charisma": 15 }, "startingAffection": 0, "startingTrust": 0 } ] }"""));

        Assert.False(result.IsOk);
        Assert.Equal(["b.json:/characters/0/archetypeId"], Paths(result));
    }

    [Fact]
    public void A_property_the_format_does_not_know_is_refused_at_its_pointer()
    {
        var result = ContentPackLoader.Load(Memory("""{ "settings": { "playerName": "P", "lineup": { "min": 1, "max": 1 }, "bogus": 3 } }"""));
        Assert.Equal(["a.json:/settings/bogus"], Paths(result));
    }

    [Fact]
    public void A_required_property_that_is_missing_is_refused_at_its_pointer()
    {
        var result = ContentPackLoader.Load(Memory("""{ "settings": { "lineup": { "min": 1, "max": 1 } } }"""));
        Assert.Contains(Paths(result), p => p.StartsWith("a.json:/settings", StringComparison.Ordinal));
    }

    [Fact]
    public void A_wrong_enum_spelling_is_refused_at_the_property()
    {
        var result = ContentPackLoader.Load(Memory(Settings, """{ "characters": [ { "id": "ann", "name": "Ann", "archetypeId": "x", "personality": { "patience": 20, "empathy": 20, "cunning": 10, "logic": 15, "kindness": 20, "charisma": 15 }, "startingAffection": 0, "startingTrust": 0, "combat": { "rank": 1, "stamina": 1, "power": 1, "speed": 1, "elementalPath": "Fire" } } ] }"""));
        Assert.Contains("b.json:/characters/0/combat/elementalPath", Paths(result));
    }

    [Fact]
    public void A_property_written_twice_is_refused()
    {
        var result = ContentPackLoader.Load(Memory("""{ "settings": { "playerName": "P", "playerName": "Q", "lineup": { "min": 1, "max": 1 } } }"""));
        Assert.False(result.IsOk);
        var detail = Assert.Single(result.Error.Details);
        Assert.Equal("a.json:line 1, column 36", detail.Path);
        Assert.Contains("playerName", detail.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unknown_type_discriminator_is_refused_at_the_item()
    {
        var conversation = """{ "conversations": [ { "id": "chat", "participants": [], "steps": [ { "id": "hello", "lines": [], "effects": [ { "type": "explode" } ] } ] } ] }""";
        var result = ContentPackLoader.Load(Memory(Settings, conversation));
        Assert.Contains(Paths(result), p => p.StartsWith("b.json:/conversations/0/steps/0", StringComparison.Ordinal));
    }

    [Fact]
    public void A_syntax_error_reports_its_line_and_column()
    {
        var result = ContentPackLoader.Load(Memory("{\n  \"settings\": { \"playerName\": \"P\" \"lineup\": {} }\n}"));
        Assert.Contains(Paths(result), p => p.StartsWith("a.json:line 2, column ", StringComparison.Ordinal));
    }

    [Fact]
    public void A_syntax_error_in_pack_json_is_reported_against_pack_json()
    {
        var result = ContentPackLoader.Load(new MemoryContentSource([KeyValuePair.Create("pack.json", "{ \"format\": ")]));
        Assert.Contains(Paths(result), p => p.StartsWith("pack.json:line 1, column ", StringComparison.Ordinal));
    }

    [Fact]
    public void Problems_across_several_files_are_all_collected_in_one_refusal()
    {
        var result = ContentPackLoader.Load(Memory(
            """{ "settings": { "playerName": "P", "lineup": { "min": 1, "max": 1 }, "bogus": 1 } }""",
            "{ \"archetypes\": [ { \"id\": \"x\" } \n \"oops\" ] }"));

        var paths = Paths(result);
        Assert.Contains(paths, p => p.StartsWith("a.json:", StringComparison.Ordinal));
        Assert.Contains(paths, p => p.StartsWith("b.json:", StringComparison.Ordinal));
    }

    [Fact]
    public void A_folder_pack_missing_its_index_or_a_listed_file_is_refused_naming_the_file()
    {
        Directory.CreateDirectory(_root);
        Assert.Equal(["pack.json"], Paths(ContentPackLoader.Load(new FolderContentSource(_root))));

        File.WriteAllText(Path.Combine(_root, "pack.json"), Index);
        File.WriteAllText(Path.Combine(_root, "a.json"), Settings);
        Assert.Equal(["b.json"], Paths(ContentPackLoader.Load(new FolderContentSource(_root))));
    }

    [Fact]
    public void A_file_with_a_null_element_in_a_list_is_refused_not_thrown()
    {
        var result = ContentPackLoader.Load(Memory(Settings, """{ "archetypes": [ null ] }"""));
        Assert.Contains(Paths(result), p => p.StartsWith("b.json:", StringComparison.Ordinal));
    }

    [Fact]
    public void Index_problems_are_reported_against_pack_json_with_pointers()
    {
        var source = new MemoryContentSource([KeyValuePair.Create("pack.json", """{ "format": "wrong", "version": 99, "id": "t", "name": "T", "standIn": false, "files": ["a.json", "a.json"] }"""), KeyValuePair.Create("a.json", Settings)]);
        var paths = Paths(ContentPackLoader.Load(source));
        Assert.All(paths, p => Assert.StartsWith("pack.json:", p, StringComparison.Ordinal));
        Assert.Contains("pack.json:/files/1", paths);
    }
}
