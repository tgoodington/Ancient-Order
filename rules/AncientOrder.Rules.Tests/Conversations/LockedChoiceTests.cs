using AncientOrder.Rules.Common;
using AncientOrder.Rules.Conditions;
using AncientOrder.Rules.Content.Packs;
using AncientOrder.Rules.Conversations;
using AncientOrder.Rules.Personality;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Tests.ConversationTesting;

/// <summary>Locked choices: refusal, the locked line, the requirement detail, inclusive thresholds. The namespace avoids shadowing the rules Conversations namespace.</summary>
public class LockedChoiceTests
{
    private const string Pack = """{ "format": "ancient-order-content", "version": 1, "id": "t", "name": "Test", "standIn": false, "files": ["base.json", "talk.json"] }""";

    private const string PackDefault = "\"conversations\": { \"lockedText\": \"Pack default line.\" }, ";

    // Starting personality: patience 20, empathy 20, cunning 10, logic 15, kindness 20, charisma 15.
    private const string Base = """
        {
          "settings": { "playerName": "Kael", "lineup": { "min": 1, "max": 1 }, "conversations": { "lockedText": "Pack default line." }, "startingPersonality": { "patience": 20, "empathy": 20, "cunning": 10, "logic": 15, "kindness": 20, "charisma": 15 } },
          "archetypes": [{ "id": "x" }],
          "characters": [],
          "fights": [ { "id": "brawl", "name": "Brawl", "enemies": [ { "id": "rat", "name": "Rat", "archetype": "x", "rank": 1, "stamina": 10, "power": 5, "speed": 5, "elementalPath": "fire", "reactionSkills": { "block": { "sr": 1, "smr": 1, "fmr": 1 }, "dodge": { "sr": 1, "smr": 1, "fmr": 1 }, "parry": { "sr": 1, "smr": 1, "fmr": 1 } } } ] } ]
        }
        """;

    private const string Talk = """
        {
          "conversations": [
            { "id": "chat", "participants": [], "start": [ { "step": "s" } ],
              "steps": [
                { "id": "s", "lines": [],
                  "choices": [
                    { "id": "open", "text": "Open", "effects": [ { "type": "shift", "cunning": 5 } ] },
                    { "id": "own", "text": "Own", "lockedText": "Its own line.", "requires": [ { "type": "trait", "trait": "cunning", "atLeast": 30 } ], "effects": [ { "type": "shift", "cunning": 5 } ] },
                    { "id": "plain", "text": "Plain", "requires": [ { "type": "trait", "trait": "cunning", "atLeast": 30 } ] },
                    { "id": "exact", "text": "Exact", "requires": [ { "type": "trait", "trait": "logic", "atLeast": 15 } ] },
                    { "id": "just_over", "text": "Just over", "requires": [ { "type": "trait", "trait": "logic", "atLeast": 15.01 } ] },
                    { "id": "ceiling_exact", "text": "Ceiling exact", "requires": [ { "type": "trait", "trait": "logic", "atMost": 15 } ] },
                    { "id": "ceiling_under", "text": "Ceiling under", "requires": [ { "type": "trait", "trait": "logic", "atMost": 14.99 } ] },
                    { "id": "band", "text": "Band", "requires": [ { "type": "trait", "trait": "logic", "atLeast": 10, "atMost": 15 } ] }
                  ] }
              ] }
          ]
        }
        """;

    private static (GameContent Content, GameState Game) Started(string baseJson = Base)
    {
        var loaded = ContentPackLoader.Load(new MemoryContentSource([KeyValuePair.Create("pack.json", Pack), KeyValuePair.Create("base.json", baseJson), KeyValuePair.Create("talk.json", Talk)]));
        Assert.True(loaded.IsOk, loaded.IsOk ? string.Empty : string.Join("\n", loaded.Error.Details));
        var game = GameStateUpdates.NewGame(loaded.Value, "p", 1);
        return (loaded.Value, ConversationRules.Start(loaded.Value, game, "chat", 2).Value.Game);
    }

    private static ChoiceView ViewOf(GameContent content, GameState game, string id) =>
        ConversationView.From(content, game)!.Choices.Single(c => c.Id == id);

    [Theory]
    [InlineData("own")]
    [InlineData("plain")]
    [InlineData("just_over")]
    [InlineData("ceiling_under")]
    public void A_locked_choice_is_refused_with_choice_unavailable(string id)
    {
        var (content, game) = Started();

        Assert.False(ViewOf(content, game, id).Available);
        var result = ConversationRules.Choose(content, game, id, 3);

        Assert.False(result.IsOk);
        Assert.Equal(ErrorCode.CHOICE_UNAVAILABLE, result.Error.Code);
    }

    [Fact]
    public void A_refused_locked_choice_leaves_the_game_exactly_as_it_was()
    {
        var (content, game) = Started();
        var before = game;

        _ = ConversationRules.Choose(content, game, "own", 3);

        Assert.Same(before, game);
        Assert.Equal(10, game.Player.Personality.Cunning);
        Assert.Equal("s", game.Conversation!.StepId);
    }

    [Fact]
    public void An_open_choice_is_available_has_no_requirement_and_is_taken()
    {
        var (content, game) = Started();
        var open = ViewOf(content, game, "open");

        Assert.True(open.Available);
        Assert.Null(open.Requirement);
        Assert.True(ConversationRules.Choose(content, game, "open", 3).IsOk);
    }

    [Fact]
    public void A_locked_choice_shows_its_own_line_or_else_the_pack_default()
    {
        var (content, game) = Started();

        Assert.Equal("Its own line.", ViewOf(content, game, "own").LockedText);
        Assert.Equal("Pack default line.", ViewOf(content, game, "plain").LockedText);
    }

    [Fact]
    public void The_pack_default_falls_back_to_the_built_in_line_when_the_pack_sets_none()
    {
        var (content, game) = Started(Base.Replace(PackDefault, string.Empty, StringComparison.Ordinal));

        Assert.Equal(ConversationSettings.DefaultLockedText, ViewOf(content, game, "plain").LockedText);
        Assert.Equal("Its own line.", ViewOf(content, game, "own").LockedText);
    }

    [Fact]
    public void A_locked_choice_requirement_carries_the_threshold_and_the_players_current_value()
    {
        var (content, game) = Started();

        var all = Assert.IsType<RequirementView.All>(ViewOf(content, game, "plain").Requirement);
        Assert.False(all.Met);
        var range = Assert.IsType<RequirementView.TraitRange>(Assert.Single(all.Children));
        Assert.Equal((Trait.Cunning, 30d, (double?)null, 10d, false), (range.Trait, range.AtLeast, range.AtMost, range.Current, range.Met));
    }

    [Fact]
    public void The_requirement_current_value_follows_a_shift()
    {
        var (content, game) = Started();
        var shifted = ConversationRules.Choose(content, game, "open", 3).Value.Game with { Conversation = game.Conversation };

        var range = Assert.IsType<RequirementView.TraitRange>(Assert.Single(Assert.IsType<RequirementView.All>(ViewOf(content, shifted, "plain").Requirement).Children));
        Assert.Equal(shifted.Player.Personality.Cunning, range.Current);
        Assert.NotEqual(10, range.Current);
    }

    [Theory]
    [InlineData("exact", true)]
    [InlineData("just_over", false)]
    [InlineData("ceiling_exact", true)]
    [InlineData("ceiling_under", false)]
    [InlineData("band", true)]
    public void Thresholds_are_inclusive(string id, bool available)
    {
        var (content, game) = Started();

        Assert.Equal(available, ViewOf(content, game, id).Available);
        Assert.Equal(available, ConversationRules.Choose(content, game, id, 3).IsOk);
    }

    [Fact]
    public void A_locked_choice_opens_when_a_shift_reaches_its_threshold()
    {
        var (content, game) = Started();
        Assert.False(ViewOf(content, game, "plain").Available);

        var shifted = game with { Player = game.Player with { Personality = game.Player.Personality.With(Trait.Cunning, 30) } };

        Assert.True(ViewOf(content, shifted, "plain").Available);
        Assert.Null(ViewOf(content, shifted, "plain").Requirement);
        Assert.True(ConversationRules.Choose(content, shifted, "plain", 3).IsOk);
    }
}
