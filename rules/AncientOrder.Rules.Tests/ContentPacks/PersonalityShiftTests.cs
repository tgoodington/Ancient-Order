using AncientOrder.Rules.Conditions;
using AncientOrder.Rules.Content.Packs;
using AncientOrder.Rules.Conversations;
using AncientOrder.Rules.Personality;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Tests.ContentPacks;

public class PersonalityShiftTests
{
    private const string Pack = """{ "format": "ancient-order-content", "version": 1, "id": "t", "name": "Test", "standIn": false, "files": ["base.json", "talk.json"] }""";

    private static string Base(string extraSettings = "") => $$"""
        {
          "settings": { "playerName": "Kael", "lineup": { "min": 1, "max": 1 }, "startingPersonality": { "patience": 20, "empathy": 20, "cunning": 10, "logic": 15, "kindness": 20, "charisma": 15 }{{extraSettings}} },
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
                    { "id": "bold", "text": "Bold", "effects": [ { "type": "shift", "cunning": 10, "logic": -3 }, { "type": "shift", "cunning": 4 } ] },
                    { "id": "extreme", "text": "Extreme", "effects": [ { "type": "shift", "cunning": 100 } ] },
                    { "id": "nothing", "text": "Nothing", "effects": [ { "type": "shift", "cunning": 0 } ] },
                    { "id": "clever", "text": "Clever", "requires": [ { "type": "trait", "trait": "cunning", "atLeast": 30 } ] },
                    { "id": "wise", "text": "Wise", "lockedText": "You hold your tongue.", "requires": [ { "type": "trait", "trait": "logic", "atLeast": 15 } ] }
                  ] }
              ] }
          ]
        }
        """;

    private static GameContent Content(string talk = Talk, string extraSettings = "")
    {
        var loaded = ContentPackLoader.Load(new MemoryContentSource([KeyValuePair.Create("pack.json", Pack), KeyValuePair.Create("base.json", Base(extraSettings)), KeyValuePair.Create("talk.json", talk)]));
        Assert.True(loaded.IsOk, loaded.IsOk ? string.Empty : string.Join("\n", loaded.Error.Details));
        return loaded.Value;
    }

    private static GameState Started(GameContent content)
    {
        var game = GameStateUpdates.NewGame(content, "p", 1);
        return ConversationRules.Start(content, game, "chat", 2).Value.Game;
    }

    private static void AssertValid(AncientOrder.Rules.Personality.Personality p)
    {
        Assert.True(PersonalityRules.Validate(p).Valid);
    }

    [Fact]
    public void Shifts_apply_in_order_stay_valid_and_the_report_lists_actual_changes_in_trait_order()
    {
        var content = Content();
        var game = Started(content);
        var step = ConversationRules.Choose(content, game, "bold", 3).Value;
        var after = step.Game.Player.Personality;
        AssertValid(after);

        Assert.Equal(PersonalityRules.Traits.Where(t => game.Player.Personality[t] != after[t]), step.Report.TraitChanges.Select(c => c.Trait));
        Assert.All(step.Report.TraitChanges, c =>
        {
            Assert.Equal(game.Player.Personality[c.Trait], c.Before);
            Assert.Equal(after[c.Trait], c.After);
        });
        Assert.Equal([Trait.Cunning, Trait.Logic], step.Report.WrittenShift.Select(s => s.Trait));
        Assert.Equal([14d, -3d], step.Report.WrittenShift.Select(s => s.Amount));
        Assert.True(step.Report.TraitChanges.First(c => c.Trait == Trait.Cunning).Change > 0);
    }

    [Fact]
    public void A_huge_shift_is_limited_to_35_and_the_report_shows_what_actually_happened()
    {
        var content = Content();
        var step = ConversationRules.Choose(content, Started(content), "extreme", 3).Value;
        AssertValid(step.Game.Player.Personality);
        Assert.Equal(35, step.Game.Player.Personality.Cunning);
        Assert.Equal(100, step.Report.WrittenShift.Single().Amount);
    }

    [Fact]
    public void A_shift_that_changes_nothing_reports_no_trait_change()
    {
        var content = Content();
        var step = ConversationRules.Choose(content, Started(content), "nothing", 3).Value;
        Assert.Empty(step.Report.TraitChanges);
    }

    [Fact]
    public void A_trait_gate_locks_the_choice_with_its_line_and_the_requirement_with_the_current_value()
    {
        var content = Content();
        var view = ConversationView.From(content, Started(content))!;
        var clever = view.Choices.Single(c => c.Id == "clever");
        Assert.False(clever.Available);
        Assert.Equal(ConversationSettings.DefaultLockedText, clever.LockedText);
        var req = Assert.IsType<RequirementView.All>(clever.Requirement).Children.Single();
        var range = Assert.IsType<RequirementView.TraitRange>(req);
        Assert.Equal((Trait.Cunning, 30d, 10d, false), (range.Trait, range.AtLeast, range.Current, range.Met));

        var wise = view.Choices.Single(c => c.Id == "wise");
        Assert.True(wise.Available);
        Assert.Null(wise.Requirement);
    }

    [Fact]
    public void A_pack_default_locked_line_and_a_choices_own_line_are_used()
    {
        var talk = Talk.Replace("\"requires\": [ { \"type\": \"trait\", \"trait\": \"logic\", \"atLeast\": 15 } ]", "\"requires\": [ { \"type\": \"trait\", \"trait\": \"logic\", \"atLeast\": 30 } ]", StringComparison.Ordinal);
        var content = Content(talk, ", \"conversations\": { \"lockedText\": \"Not now.\" }");
        var view = ConversationView.From(content, Started(content))!;
        Assert.Equal("Not now.", view.Choices.Single(c => c.Id == "clever").LockedText);
        Assert.Equal("You hold your tongue.", view.Choices.Single(c => c.Id == "wise").LockedText);
    }

    [Fact]
    public void A_gate_opens_once_the_trait_holds()
    {
        var content = Content();
        var game = Started(content);
        var shifted = ConversationRules.Choose(content, game, "extreme", 3).Value.Game;
        var reopened = shifted with { Conversation = game.Conversation };
        var clever = ConversationView.From(content, reopened)!.Choices.Single(c => c.Id == "clever");
        Assert.True(clever.Available);
        Assert.True(ConversationRules.Choose(content, reopened, "clever", 4).IsOk);
    }

    [Theory]
    [InlineData("{ \"type\": \"shift\", \"cunnning\": 3 }")]
    [InlineData("{ \"type\": \"shift\", \"cunning\": 1e999 }")]
    [InlineData("{ \"type\": \"shift\", \"cunning\": \"a lot\" }")]
    public void A_misspelled_or_non_finite_shift_is_refused_by_the_loader(string effect)
    {
        var talk = Talk.Replace("{ \"type\": \"shift\", \"cunning\": 100 }", effect, StringComparison.Ordinal);
        var loaded = ContentPackLoader.Load(new MemoryContentSource([KeyValuePair.Create("pack.json", Pack), KeyValuePair.Create("base.json", Base()), KeyValuePair.Create("talk.json", talk)]));
        Assert.False(loaded.IsOk);
    }
}
