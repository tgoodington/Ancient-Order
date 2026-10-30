using AncientOrder.Rules.Content.Packs;
using AncientOrder.Rules.Conversations;
using AncientOrder.Rules.Reactions;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Tests.ReactionTesting;

// ContentPacks/ReactionTests.cs already covers: two archetypes reacting differently, participant order, the minimum, a capped trait still reacting,
// a clamped character reporting intended versus applied, silence and replace overrides, an override without a shift, extreme values, an override naming a non-participant,
// and the first demeanor cutoffs. This file adds the boundaries and the remaining rules.
public class ReactionRulesTests
{
    private const string Pack = """{ "format": "ancient-order-content", "version": 1, "id": "t", "name": "Test", "standIn": false, "files": ["base.json", "talk.json"] }""";

    private const string Even = """{ "patience": 17, "empathy": 17, "cunning": 17, "logic": 17, "kindness": 15, "charisma": 17 }""";

    private static string Archetype(string id, double lean, double minimum, double affection, double trust) => $$"""
        { "id": "{{id}}", "reactions": { "leanings": { "kindness": {{lean}} }, "minimum": {{minimum}}, "affectionPerPoint": {{affection}}, "trustPerPoint": {{trust}},
          "approve": { "clear": "{name} approves.", "slight": "{name} nods.", "strong": "{name} cheers." },
          "disapprove": { "clear": "{name} disapproves." } } }
        """;

    private static string Person(string id, string archetype, double affection = 0, double trust = 0) =>
        $$"""{ "id": "{{id}}", "name": "{{id}}", "archetypeId": "{{archetype}}", "personality": {{Even}}, "startingAffection": {{affection}}, "startingTrust": {{trust}} }""";

    private static string Base(string settings = "", string extraPeople = "") => $$"""
        {
          "settings": { "playerName": "Kael", "lineup": { "min": 1, "max": 1 }, "startingPersonality": {{Even}}{{settings}} },
          "archetypes": [
            {{Archetype("unit", 1, 0, 1, 0)}},
            {{Archetype("trusty", 1, 0, 0.1, 1)}},
            {{Archetype("gated", 1, 4, 1, 0)}},
            {{Archetype("faint", 1, 0, 0.04, 0.04)}},
            {{Archetype("half", 1, 0, 0.25, 0)}},
            {{Archetype("halfneg", -1, 0, 0.25, 0)}},
            { "id": "plain" }
          ],
          "characters": [
            {{Person("ann", "unit")}},
            {{Person("tru", "trusty")}},
            {{Person("gat", "gated")}},
            {{Person("fai", "faint")}},
            {{Person("hal", "half")}},
            {{Person("neg", "halfneg")}},
            {{Person("nob", "plain")}}{{extraPeople}}
          ],
          "fights": []
        }
        """;

    private static string Shift(string id, int amount, string reactions = "") =>
        $$"""{ "id": "{{id}}", "text": "{{id}}", "effects": [ { "type": "shift", "kindness": {{amount}} } ]{{reactions}} }""";

    private const string Everyone = """["ann", "tru", "gat", "fai", "hal", "neg", "nob"]""";

    private static string Talk(params string[] choices) => $$"""
        {
          "conversations": [
            { "id": "chat", "participants": {{Everyone}}, "start": [ { "step": "s" } ],
              "steps": [ { "id": "s", "lines": [], "choices": [ {{string.Join(",", choices)}} ] } ] }
          ]
        }
        """;

    private static readonly string[] AllChoices =
    [
        Shift("s1", 1), Shift("s2", 2), Shift("s3", 3), Shift("s4", 4), Shift("s7", 7), Shift("s8", 8), Shift("s10", 10),
        Shift("replace", 10, """, "reactions": [ { "character": "ann", "text": "{name} turns away.", "affection": 5, "trust": -9 } ]"""),
        Shift("flat", 10, """, "reactions": [ { "character": "ann", "text": "{name} is torn.", "affection": 5, "trust": -5 } ]"""),
        Shift("faintwrite", 10, """, "reactions": [ { "character": "ann", "text": "{name} blinks.", "affection": 2, "trust": 1 } ]"""),
        Shift("trustwrite", 10, """, "reactions": [ { "character": "ann", "text": "{name} doubts you.", "affection": 1, "trust": -8 } ]"""),
        """{ "id": "textonly", "text": "textonly", "reactions": [ { "character": "ann", "text": "{name} sighs, says {name}." } ] }""",
    ];

    private static GameContent Load(string baseJson, string talkJson)
    {
        var loaded = ContentPackLoader.Load(new MemoryContentSource([KeyValuePair.Create("pack.json", Pack), KeyValuePair.Create("base.json", baseJson), KeyValuePair.Create("talk.json", talkJson)]));
        Assert.True(loaded.IsOk, loaded.IsOk ? string.Empty : string.Join("\n", loaded.Error.Details));
        return loaded.Value;
    }

    private static GameContent Content(string settings = "", string extraPeople = "") => Load(Base(settings, extraPeople), Talk(AllChoices));

    private static ConversationStep Choose(GameContent content, string choice, GameState? from = null)
    {
        var game = from ?? GameStateUpdates.NewGame(content, "p", 1);
        var started = ConversationRules.Start(content, game, "chat", 2).Value.Game;
        var step = ConversationRules.Choose(content, started, choice, 3);
        Assert.True(step.IsOk, step.IsOk ? string.Empty : step.Error.Message);
        return step.Value;
    }

    private static ReactionReport? Of(ConversationStep step, string id) => step.Report.Reactions.SingleOrDefault(r => r.CharacterId == id);

    [Theory]
    [InlineData("s2", TellStrength.Slight)]
    [InlineData("s3", TellStrength.Clear)]
    [InlineData("s7", TellStrength.Clear)]
    [InlineData("s8", TellStrength.Strong)]
    public void Strength_changes_at_clearAt_and_strongAt(string choice, TellStrength expected)
    {
        var report = Of(Choose(Content(), choice), "ann")!;
        Assert.Equal(expected, report.Strength);
        Assert.Equal(TellDirection.Warmer, report.Direction);
    }

    [Theory]
    [InlineData("s2", "ann nods.")]
    [InlineData("s3", "ann approves.")]
    [InlineData("s8", "ann cheers.")]
    public void The_line_is_chosen_by_strength(string choice, string line)
    {
        Assert.Equal(line, Of(Choose(Content(), choice), "ann")!.Text);
    }

    [Fact]
    public void A_missing_slight_or_strong_line_falls_back_to_the_clear_line()
    {
        var content = Content();
        Assert.Equal("neg disapproves.", Of(Choose(content, "s10"), "neg")!.Text);

        var trust = Of(Choose(content, "s8"), "tru")!;
        Assert.Equal((TellStrength.Strong, "tru cheers."), (trust.Strength, trust.Text));
    }

    [Fact]
    public void The_larger_of_affection_and_trust_sets_the_strength()
    {
        var tru = Of(Choose(Content(), "s8"), "tru")!;
        Assert.Equal((1d, 8d), (tru.IntendedAffection, tru.IntendedTrust));
        Assert.Equal(TellStrength.Strong, tru.Strength);
    }

    [Fact]
    public void The_cutoffs_are_content_so_the_same_choice_changes_strength()
    {
        var tight = Content(""", "tells": { "clearAt": 1, "strongAt": 2 }""");
        Assert.Equal(TellStrength.Strong, Of(Choose(tight, "s2"), "ann")!.Strength);
        Assert.Equal(TellStrength.Clear, Of(Choose(tight, "s1"), "ann")!.Strength);

        var wide = Content(""", "tells": { "clearAt": 5, "strongAt": 10 }""");
        Assert.Equal(TellStrength.Slight, Of(Choose(wide, "s4"), "ann")!.Strength);
        Assert.Equal(TellStrength.Clear, Of(Choose(wide, "s7"), "ann")!.Strength);
    }

    [Fact]
    public void The_tell_cutoffs_must_be_ordered()
    {
        foreach (var bad in new[] { """, "tells": { "clearAt": 9, "strongAt": 3 }""", """, "tells": { "clearAt": -1, "strongAt": 3 }""" })
        {
            var loaded = ContentPackLoader.Load(new MemoryContentSource([KeyValuePair.Create("pack.json", Pack), KeyValuePair.Create("base.json", Base(bad)), KeyValuePair.Create("talk.json", Talk(AllChoices))]));
            Assert.False(loaded.IsOk, bad);
            Assert.Contains(loaded.Error.Details, d => d.Message.Contains("clearAt", StringComparison.Ordinal));
        }

        var equal = Content(""", "tells": { "clearAt": 4, "strongAt": 4 }""");
        Assert.Equal(TellStrength.Strong, Of(Choose(equal, "s4"), "ann")!.Strength);
    }

    [Fact]
    public void Changes_are_rounded_like_javascript_half_toward_positive()
    {
        var content = Content();
        var up = Of(Choose(content, "s10"), "hal")!;
        Assert.Equal((3d, 0d), (up.IntendedAffection, up.IntendedTrust));
        Assert.Equal(3d, up.AffectionChange);

        var down = Of(Choose(content, "s10"), "neg")!;
        Assert.Equal(ReactionKind.Disapprove, down.Kind);
        Assert.Equal((-2d, 0d), (down.IntendedAffection, down.IntendedTrust));
    }

    [Fact]
    public void No_reaction_when_both_changes_round_to_zero_even_above_the_minimum()
    {
        var content = Content();
        Assert.Null(Of(Choose(content, "s10"), "fai"));
        Assert.NotNull(Of(Choose(content, "s10"), "hal"));
    }

    [Fact]
    public void The_minimum_is_inclusive_in_size()
    {
        var content = Content();
        Assert.Null(Of(Choose(content, "s3"), "gat"));
        Assert.NotNull(Of(Choose(content, "s4"), "gat"));
    }

    [Fact]
    public void A_character_without_reactions_never_reacts_to_a_shift()
    {
        Assert.Null(Of(Choose(Content(), "s10"), "nob"));
    }

    [Fact]
    public void A_written_override_replaces_the_archetype_text_and_values()
    {
        var content = Content();
        var archetype = Of(Choose(content, "s10"), "ann")!;
        Assert.Equal((ReactionKind.Approve, "ann cheers."), (archetype.Kind, archetype.Text));

        var written = Of(Choose(content, "replace"), "ann")!;
        Assert.Equal((ReactionKind.Written, "ann turns away.", 5d, -9d), (written.Kind, written.Text, written.IntendedAffection, written.IntendedTrust));
        Assert.Equal((TellDirection.Cooler, TellStrength.Strong), (written.Direction, written.Strength));
    }

    [Fact]
    public void A_written_override_takes_its_direction_from_the_sum_and_its_strength_from_the_larger_change()
    {
        var content = Content();
        var flat = Of(Choose(content, "flat"), "ann")!;
        Assert.Equal((TellDirection.Neutral, TellStrength.Slight), (flat.Direction, flat.Strength));
        Assert.Equal((5d, -5d), (flat.AffectionChange, flat.TrustChange));

        var small = Of(Choose(content, "faintwrite"), "ann")!;
        Assert.Equal((TellDirection.Warmer, TellStrength.Slight), (small.Direction, small.Strength));

        var distrust = Of(Choose(content, "trustwrite"), "ann")!;
        Assert.Equal((TellDirection.Cooler, TellStrength.Strong), (distrust.Direction, distrust.Strength));
    }

    [Fact]
    public void Name_is_substituted_in_every_occurrence_for_archetype_and_written_lines()
    {
        var content = Content();
        Assert.Equal("ann sighs, says ann.", Of(Choose(content, "textonly"), "ann")!.Text);
        Assert.Equal("hal approves.", Of(Choose(content, "s10"), "hal")!.Text);
    }

    [Fact]
    public void Applied_change_stops_at_the_range_while_the_intended_change_is_kept()
    {
        var content = Content();
        var game = GameStateUpdates.NewGame(content, "p", 1);
        game = game with { Characters = game.Characters.SetItem("ann", new CharacterState(97, -98)) };

        var reaction = Of(Choose(content, "replace", game), "ann")!;
        Assert.Equal((5d, -9d), (reaction.IntendedAffection, reaction.IntendedTrust));
        Assert.Equal((3d, -2d), (reaction.AffectionChange, reaction.TrustChange));
    }

    [Fact]
    public void A_character_at_the_cap_still_reacts_and_stays_in_range()
    {
        var content = Content();
        var game = GameStateUpdates.NewGame(content, "p", 1);
        game = game with { Characters = game.Characters.SetItem("ann", new CharacterState(100, 100)) };

        var step = Choose(content, "s8", game);
        var reaction = Of(step, "ann")!;
        Assert.Equal((TellDirection.Warmer, TellStrength.Strong, 8d), (reaction.Direction, reaction.Strength, reaction.IntendedAffection));
        Assert.Equal((0d, 0d), (reaction.AffectionChange, reaction.TrustChange));
        Assert.Equal((100d, 100d), (step.Game.Characters["ann"].Affection, step.Game.Characters["ann"].Trust));
    }

    [Fact]
    public void Relationships_stay_within_range_after_a_run_of_choices()
    {
        var content = Content();
        var game = GameStateUpdates.NewGame(content, "p", 1);
        for (var i = 0; i < 40; i++)
        {
            var started = ConversationRules.Start(content, game, "chat", 2 + i).Value.Game;
            game = ConversationRules.Choose(content, started, i % 2 == 0 ? "replace" : "s10", 3 + i).Value.Game;
            foreach (var state in game.Characters.Values)
            {
                Assert.InRange(state.Affection, -100, 100);
                Assert.InRange(state.Trust, -100, 100);
            }
        }

        Assert.Equal(100d, game.Characters["hal"].Affection);
        Assert.Equal(-100d, game.Characters["ann"].Trust);
    }

    [Fact]
    public void Archetypes_and_character_definitions_are_unchanged_after_play()
    {
        var content = Content();
        var before = content.Archetypes.TryGet("unit", out var archetype) ? archetype : null;
        content.Characters.TryGet("ann", out var annBefore);

        var game = GameStateUpdates.NewGame(content, "p", 1);
        for (var i = 0; i < 5; i++)
        {
            var started = ConversationRules.Start(content, game, "chat", 2 + i).Value.Game;
            game = ConversationRules.Choose(content, started, "s10", 3 + i).Value.Game;
        }

        Assert.NotEqual(0d, game.Characters["ann"].Affection);
        Assert.True(content.Archetypes.TryGet("unit", out var after));
        Assert.Equal(before, after);
        Assert.True(content.Characters.TryGet("ann", out var annAfter));
        Assert.Equal(annBefore, annAfter);
        Assert.Equal("unit", annAfter!.ArchetypeId);
    }
}
