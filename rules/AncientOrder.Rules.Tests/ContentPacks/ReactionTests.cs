using AncientOrder.Rules.Content.Packs;
using AncientOrder.Rules.Conversations;
using AncientOrder.Rules.Reactions;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Tests.ContentPacks;

public class ReactionTests
{
    private const string Pack = """{ "format": "ancient-order-content", "version": 1, "id": "t", "name": "Test", "standIn": false, "files": ["base.json", "talk.json"] }""";

    private const string EvenPersonality = """{ "patience": 17, "empathy": 17, "cunning": 17, "logic": 17, "kindness": 15, "charisma": 17 }""";

    private const string CappedKindness = """{ "patience": 13, "empathy": 13, "cunning": 13, "logic": 13, "kindness": 35, "charisma": 13 }""";

    private static string Base(string personality, string extraSettings = "") => $$"""
        {
          "settings": { "playerName": "Kael", "lineup": { "min": 1, "max": 1 }, "startingPersonality": {{personality}}{{extraSettings}} },
          "archetypes": [
            { "id": "kind", "reactions": { "leanings": { "kindness": 1 }, "minimum": 2, "affectionPerPoint": 0.5, "trustPerPoint": 0.25,
              "approve": { "clear": "{name} smiles.", "slight": "{name} nods.", "strong": "{name} beams." },
              "disapprove": { "clear": "{name} frowns." } } },
            { "id": "sly", "reactions": { "leanings": { "kindness": -1 }, "minimum": 2, "affectionPerPoint": 0.5, "trustPerPoint": 0.25,
              "approve": { "clear": "{name} grins." },
              "disapprove": { "clear": "{name} sneers.", "strong": "{name} recoils." } } },
            { "id": "plain" }
          ],
          "characters": [
            { "id": "ann", "name": "Ann", "archetypeId": "kind", "personality": {{EvenPersonality}}, "startingAffection": 0, "startingTrust": 0 },
            { "id": "bo", "name": "Bo", "archetypeId": "sly", "personality": {{EvenPersonality}}, "startingAffection": 0, "startingTrust": 0 },
            { "id": "cy", "name": "Cy", "archetypeId": "plain", "personality": {{EvenPersonality}}, "startingAffection": 0, "startingTrust": 0 },
            { "id": "di", "name": "Di", "archetypeId": "kind", "personality": {{EvenPersonality}}, "startingAffection": 100, "startingTrust": 100 }
          ],
          "fights": []
        }
        """;

    private const string Talk = """
        {
          "conversations": [
            { "id": "chat", "participants": ["ann", "bo", "cy", "di"], "start": [ { "step": "s" } ],
              "steps": [
                { "id": "s", "lines": [],
                  "choices": [
                    { "id": "nice", "text": "Nice", "effects": [ { "type": "shift", "kindness": 10 } ] },
                    { "id": "big", "text": "Big", "effects": [ { "type": "shift", "kindness": 20 } ] },
                    { "id": "small", "text": "Small", "effects": [ { "type": "shift", "kindness": 4 } ] },
                    { "id": "tiny", "text": "Tiny", "effects": [ { "type": "shift", "kindness": 1 } ] },
                    { "id": "mixed", "text": "Mixed", "effects": [ { "type": "shift", "kindness": 10 } ],
                      "reactions": [ { "character": "ann", "silence": true }, { "character": "bo", "text": "{name} shrugs.", "affection": 0 }, { "character": "cy", "text": "Cy cheers.", "affection": 9, "trust": -2 } ] },
                    { "id": "quiet", "text": "Quiet", "reactions": [ { "character": "cy", "text": "Cy watches." } ] }
                  ] }
              ] }
          ]
        }
        """;

    private static GameContent Content(string personality = EvenPersonality, string talk = Talk, string extraSettings = "")
    {
        var loaded = ContentPackLoader.Load(new MemoryContentSource([KeyValuePair.Create("pack.json", Pack), KeyValuePair.Create("base.json", Base(personality, extraSettings)), KeyValuePair.Create("talk.json", talk)]));
        Assert.True(loaded.IsOk, loaded.IsOk ? string.Empty : string.Join("\n", loaded.Error.Details));
        return loaded.Value;
    }

    private static ConversationStep Choose(GameContent content, string choice)
    {
        var game = GameStateUpdates.NewGame(content, "p", 1);
        var started = ConversationRules.Start(content, game, "chat", 2).Value.Game;
        var step = ConversationRules.Choose(content, started, choice, 3);
        Assert.True(step.IsOk, step.IsOk ? string.Empty : step.Error.Message);
        return step.Value;
    }

    [Fact]
    public void Archetypes_react_differently_to_the_same_choice_in_participant_order_and_change_the_relationship()
    {
        var content = Content();
        var step = Choose(content, "nice");

        Assert.Equal(["ann", "bo", "di"], step.Report.Reactions.Select(r => r.CharacterId));
        var ann = step.Report.Reactions[0];
        Assert.Equal(("Ann", ReactionKind.Approve, TellDirection.Warmer, TellStrength.Clear, "Ann smiles."), (ann.Name, ann.Kind, ann.Direction, ann.Strength, ann.Text));
        Assert.Equal((5d, 3d), (ann.AffectionChange, ann.TrustChange));
        Assert.Equal((5d, 3d), (step.Game.Characters["ann"].Affection, step.Game.Characters["ann"].Trust));

        var bo = step.Report.Reactions[1];
        Assert.Equal((ReactionKind.Disapprove, TellDirection.Cooler, TellStrength.Clear, "Bo sneers."), (bo.Kind, bo.Direction, bo.Strength, bo.Text));
        Assert.Equal((-5d, -2d), (bo.AffectionChange, bo.TrustChange));
        Assert.Equal((0d, 0d), (step.Game.Characters["cy"].Affection, step.Game.Characters["cy"].Trust));
    }

    [Fact]
    public void Strength_picks_the_line_and_falls_back_to_the_clear_line()
    {
        var content = Content();
        var big = Choose(content, "big").Report.Reactions;
        Assert.Equal((TellStrength.Strong, "Ann beams."), (big[0].Strength, big[0].Text));
        Assert.Equal((TellStrength.Strong, "Bo recoils."), (big[1].Strength, big[1].Text));

        var small = Choose(content, "small").Report.Reactions[0];
        Assert.Equal((TellStrength.Slight, "Ann nods."), (small.Strength, small.Text));
    }

    [Fact]
    public void A_score_below_the_minimum_draws_no_reaction()
    {
        Assert.Empty(Choose(Content(), "tiny").Report.Reactions);
    }

    [Fact]
    public void A_choice_still_draws_reactions_when_the_trait_is_already_at_the_cap()
    {
        var step = Choose(Content(CappedKindness), "nice");
        Assert.Empty(step.Report.TraitChanges);
        Assert.Equal(["ann", "bo", "di"], step.Report.Reactions.Select(r => r.CharacterId));
        Assert.Equal(5d, step.Report.Reactions[0].AffectionChange);
    }

    [Fact]
    public void Changes_are_clamped_and_the_report_shows_what_was_applied()
    {
        var di = Choose(Content(), "nice").Report.Reactions.Single(r => r.CharacterId == "di");
        Assert.Equal((5d, 3d, 0d, 0d), (di.IntendedAffection, di.IntendedTrust, di.AffectionChange, di.TrustChange));
        Assert.Equal(TellDirection.Warmer, di.Direction);
    }

    [Fact]
    public void A_choice_can_silence_or_replace_a_reaction()
    {
        var step = Choose(Content(), "mixed");
        Assert.Equal(["bo", "cy", "di"], step.Report.Reactions.Select(r => r.CharacterId));

        var bo = step.Report.Reactions[0];
        Assert.Equal((ReactionKind.Written, TellDirection.Neutral, TellStrength.Slight, "Bo shrugs."), (bo.Kind, bo.Direction, bo.Strength, bo.Text));
        Assert.Equal((0d, 0d), (bo.AffectionChange, bo.TrustChange));

        var cy = step.Report.Reactions[1];
        Assert.Equal((ReactionKind.Written, TellDirection.Warmer, TellStrength.Strong, 9d, -2d), (cy.Kind, cy.Direction, cy.Strength, cy.AffectionChange, cy.TrustChange));
        Assert.Equal((9d, -2d), (step.Game.Characters["cy"].Affection, step.Game.Characters["cy"].Trust));
    }

    [Fact]
    public void An_override_fires_without_a_personality_shift()
    {
        var step = Choose(Content(), "quiet");
        var cy = Assert.Single(step.Report.Reactions);
        Assert.Equal((ReactionKind.Written, TellDirection.Neutral, "Cy watches."), (cy.Kind, cy.Direction, cy.Text));
    }

    [Fact]
    public void Extreme_content_values_are_limited_so_the_choice_still_applies()
    {
        var extreme = Base(EvenPersonality)
            .Replace("\"leanings\": { \"kindness\": 1 }", "\"leanings\": { \"kindness\": 1e200 }", StringComparison.Ordinal)
            .Replace("\"affectionPerPoint\": 0.5", "\"affectionPerPoint\": 1e200", StringComparison.Ordinal);
        var loaded = ContentPackLoader.Load(new MemoryContentSource([KeyValuePair.Create("pack.json", Pack), KeyValuePair.Create("base.json", extreme), KeyValuePair.Create("talk.json", Talk)]));
        Assert.True(loaded.IsOk);
        var step = Choose(loaded.Value, "nice");
        var ann = step.Report.Reactions[0];
        Assert.Equal((TellStrength.Strong, 200d), (ann.Strength, ann.IntendedAffection));
        Assert.Equal(100d, step.Game.Characters["ann"].Affection);
    }

    [Fact]
    public void An_override_must_name_a_participant()
    {
        var talk = Talk.Replace("\"character\": \"cy\", \"text\": \"Cy watches.\"", "\"character\": \"zed\", \"text\": \"Nobody.\"", StringComparison.Ordinal);
        var loaded = ContentPackLoader.Load(new MemoryContentSource([KeyValuePair.Create("pack.json", Pack), KeyValuePair.Create("base.json", Base(EvenPersonality)), KeyValuePair.Create("talk.json", talk)]));
        Assert.False(loaded.IsOk);
        Assert.Contains(loaded.Error.Details, d => d.Message.Contains("participant", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(60, 0, Demeanor.Warm)]
    [InlineData(50, 0, Demeanor.Warm)]
    [InlineData(49, 0, Demeanor.Neutral)]
    [InlineData(0, 0, Demeanor.Neutral)]
    [InlineData(-10, -10, Demeanor.Wary)]
    [InlineData(-30, -30, Demeanor.Cold)]
    [InlineData(-40, -20, Demeanor.Cold)]
    public void Demeanor_follows_affection_plus_trust(double affection, double trust, Demeanor expected)
    {
        Assert.Equal(expected, DemeanorRules.Of(Content().Settings, new CharacterState(affection, trust)));
    }

    [Fact]
    public void Participants_in_the_view_carry_their_demeanor()
    {
        var content = Content();
        var game = GameStateUpdates.NewGame(content, "p", 1);
        var view = ConversationView.From(content, ConversationRules.Start(content, game, "chat", 2).Value.Game)!;
        Assert.Equal([Demeanor.Neutral, Demeanor.Neutral, Demeanor.Neutral, Demeanor.Warm], view.Participants.Select(p => p.Demeanor));
    }

    [Fact]
    public void The_cutoffs_are_content_and_must_be_ordered()
    {
        var content = Content(extraSettings: """, "tells": { "clearAt": 6, "strongAt": 12 }, "demeanor": { "warmAt": 10, "waryAt": -5, "coldAt": -10 }""");
        Assert.Equal(TellStrength.Slight, Choose(content, "nice").Report.Reactions[0].Strength);
        Assert.Equal(Demeanor.Warm, DemeanorRules.Of(content.Settings, new CharacterState(10, 0)));

        var bad = ContentPackLoader.Load(new MemoryContentSource([KeyValuePair.Create("pack.json", Pack), KeyValuePair.Create("base.json", Base(EvenPersonality, """, "demeanor": { "warmAt": 10, "waryAt": 20, "coldAt": -10 }""")), KeyValuePair.Create("talk.json", Talk)]));
        Assert.False(bad.IsOk);
    }
}
