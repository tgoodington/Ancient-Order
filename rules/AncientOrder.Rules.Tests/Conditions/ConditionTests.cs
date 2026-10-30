using System.Collections.Immutable;
using AncientOrder.Rules.Conditions;
using AncientOrder.Rules.Content.Packs;
using AncientOrder.Rules.Personality;
using AncientOrder.Rules.State;
using Eval = AncientOrder.Rules.Conditions.Conditions;

// The folder is Conditions but the namespace is not: a test namespace named like a rules namespace shadows its types.
namespace AncientOrder.Rules.Tests.ConditionTesting;

/// <summary>Small focused packs for the condition tests: two characters (Ann fights), one fight, and whatever the case adds in extra.json.</summary>
internal static class ConditionPack
{
    private const string Index = """{ "format": "ancient-order-content", "version": 1, "id": "t", "name": "Test", "standIn": false, "files": ["base.json", "extra.json"] }""";

    private const string Rat = """{ "id": "rat", "name": "Rat", "archetype": "x", "rank": 1, "stamina": 10, "power": 5, "speed": 5, "elementalPath": "fire", "reactionSkills": { "block": { "sr": 1, "smr": 1, "fmr": 1 }, "dodge": { "sr": 1, "smr": 1, "fmr": 1 }, "parry": { "sr": 1, "smr": 1, "fmr": 1 } } }""";

    private const string Base = """
        {
          "settings": { "playerName": "Kael", "lineup": { "min": 1, "max": 1 }START },
          "archetypes": [{ "id": "x" }],
          "characters": [
            { "id": "ann", "name": "Ann", "archetypeId": "x", "personality": { "patience": 20, "empathy": 20, "cunning": 10, "logic": 15, "kindness": 20, "charisma": 15 }, "startingAffection": 10, "startingTrust": -5, "combat": { "rank": 1, "stamina": 10, "power": 5, "speed": 5, "elementalPath": "fire" } },
            { "id": "bo", "name": "Bo", "archetypeId": "x", "personality": { "patience": 20, "empathy": 20, "cunning": 10, "logic": 15, "kindness": 20, "charisma": 15 }, "startingAffection": 0, "startingTrust": 0 }
          ],
          "fights": [ { "id": "brawl", "name": "Brawl", "enemies": [ RAT ] } ]
        }
        """;

    public static GameContent Load(string extra = "{}", string? startingTown = null)
    {
        var start = startingTown is null ? string.Empty : $", \"startingTown\": \"{startingTown}\"";
        var baseFile = Base.Replace("START", start, StringComparison.Ordinal).Replace("RAT", Rat, StringComparison.Ordinal);
        var loaded = ContentPackLoader.Load(new MemoryContentSource([
            KeyValuePair.Create("pack.json", Index),
            KeyValuePair.Create("base.json", baseFile),
            KeyValuePair.Create("extra.json", extra),
        ]));
        Assert.True(loaded.IsOk, loaded.IsOk ? string.Empty : string.Join("\n", loaded.Error.Details));
        return loaded.Value;
    }

    public static GameState NewGame(GameContent content) => GameStateUpdates.NewGame(content, "p", 1);
}

public class ConditionTests
{
    private static readonly GameContent Content = ConditionPack.Load();

    private static GameState Game() => ConditionPack.NewGame(Content);

    private static bool Holds(GameState game, params Condition[] conditions) => Eval.Holds(Content, game, [.. conditions]);

    private static GameState WithMemory(Func<AncientOrder.Rules.Story.StoryMemory, AncientOrder.Rules.Story.StoryMemory> change)
    {
        var game = Game();
        return game with { Memory = change(game.Memory) };
    }

    [Fact]
    public void Flag_holds_only_while_the_flag_is_set()
    {
        Assert.False(Holds(Game(), new Condition.Flag("f")));
        Assert.True(Holds(WithMemory(m => m.WithFlag("f")), new Condition.Flag("f")));
        Assert.False(Holds(WithMemory(m => m.WithFlag("f").WithoutFlag("f")), new Condition.Flag("f")));
        Assert.False(Holds(WithMemory(m => m.WithFlag("F")), new Condition.Flag("f")));
    }

    [Fact]
    public void Chose_holds_only_for_that_choice_of_that_conversation()
    {
        var game = WithMemory(m => m.WithChosen("c", "pick"));
        Assert.True(Holds(game, new Condition.Chose("c", "pick")));
        Assert.False(Holds(game, new Condition.Chose("c", "other")));
        Assert.False(Holds(game, new Condition.Chose("d", "pick")));
        Assert.False(Holds(Game(), new Condition.Chose("c", "pick")));
    }

    [Fact]
    public void Seen_holds_once_the_conversation_is_remembered()
    {
        Assert.False(Holds(Game(), new Condition.Seen("c")));
        var game = WithMemory(m => m.WithSeen("c"));
        Assert.True(Holds(game, new Condition.Seen("c")));
        Assert.False(Holds(game, new Condition.Seen("d")));
    }

    [Fact]
    public void Trait_range_is_inclusive_at_both_ends_and_open_ended_when_one_bound_is_missing()
    {
        var game = Game();
        var logic = game.Player.Personality[Trait.Logic];

        Assert.True(Holds(game, new Condition.TraitRange(Trait.Logic, logic, null)));
        Assert.True(Holds(game, new Condition.TraitRange(Trait.Logic, null, logic)));
        Assert.True(Holds(game, new Condition.TraitRange(Trait.Logic, logic, logic)));
        Assert.False(Holds(game, new Condition.TraitRange(Trait.Logic, logic + 0.01, null)));
        Assert.False(Holds(game, new Condition.TraitRange(Trait.Logic, null, logic - 0.01)));
        Assert.True(Holds(game, new Condition.TraitRange(Trait.Logic, logic - 1, logic + 1)));
        Assert.False(Holds(game, new Condition.TraitRange(Trait.Logic, logic + 1, logic + 2)));
    }

    [Fact]
    public void Trait_range_reads_the_players_current_personality()
    {
        var game = Game();
        var before = game.Player.Personality[Trait.Cunning];
        var shifted = GameStateUpdates.AdjustPlayerPersonality(
            game, new PersonalityAdjustment(ImmutableDictionary<Trait, double>.Empty.Add(Trait.Cunning, 5)), 2);
        var after = shifted.Player.Personality[Trait.Cunning];

        Assert.True(after > before);
        var atLeastAfter = new Condition.TraitRange(Trait.Cunning, after, null);
        Assert.False(Holds(game, atLeastAfter));
        Assert.True(Holds(shifted, atLeastAfter));
    }

    [Fact]
    public void Relationship_range_is_inclusive_reads_the_named_measure_and_fails_for_an_unknown_character()
    {
        var game = Game();

        Assert.True(Holds(game, new Condition.RelationshipRange("ann", RelationshipMeasure.Affection, 10, null)));
        Assert.True(Holds(game, new Condition.RelationshipRange("ann", RelationshipMeasure.Affection, null, 10)));
        Assert.False(Holds(game, new Condition.RelationshipRange("ann", RelationshipMeasure.Affection, 10.5, null)));
        Assert.False(Holds(game, new Condition.RelationshipRange("ann", RelationshipMeasure.Affection, null, 9.5)));
        Assert.True(Holds(game, new Condition.RelationshipRange("ann", RelationshipMeasure.Trust, -5, -5)));
        Assert.False(Holds(game, new Condition.RelationshipRange("ann", RelationshipMeasure.Trust, 0, null)));
        Assert.False(Holds(game, new Condition.RelationshipRange("zed", RelationshipMeasure.Affection, null, 100)));
    }

    [Fact]
    public void Relationship_range_follows_changes_to_the_character()
    {
        var game = Game();
        var warmer = game with { Characters = game.Characters.SetItem("ann", game.Characters["ann"] with { Affection = 50 }) };
        var condition = new Condition.RelationshipRange("ann", RelationshipMeasure.Affection, 50, null);

        Assert.False(Holds(game, condition));
        Assert.True(Holds(warmer, condition));
    }

    [Fact]
    public void InParty_holds_for_members_only()
    {
        var game = Game() with { Party = ["ann"] };

        Assert.True(Holds(game, new Condition.InParty("ann")));
        Assert.False(Holds(game, new Condition.InParty("bo")));
        Assert.False(Holds(Game(), new Condition.InParty("ann")));
    }

    [Fact]
    public void LineupReady_needs_a_lineup_of_party_members_who_can_fight_with_or_without_a_fight_named()
    {
        var ready = Game() with { Party = ["ann"], Lineup = ["ann"] };
        var empty = Game() with { Party = ["ann"] };
        var cannotFight = Game() with { Party = ["bo"], Lineup = ["bo"] };
        var notInParty = Game() with { Lineup = ["ann"] };

        foreach (var fight in new string?[] { null, "brawl" })
        {
            var condition = new Condition.LineupReady(fight);
            Assert.True(Holds(ready, condition));
            Assert.False(Holds(empty, condition));
            Assert.False(Holds(cannotFight, condition));
            Assert.False(Holds(notInParty, condition));
        }
    }

    [Fact]
    public void All_needs_every_condition_Any_needs_one_and_Not_flips_the_result()
    {
        var game = WithMemory(m => m.WithFlag("f"));
        Condition yes = new Condition.Flag("f");
        Condition no = new Condition.Flag("g");

        Assert.True(Holds(game, new Condition.All([yes, yes])));
        Assert.False(Holds(game, new Condition.All([yes, no])));
        Assert.True(Holds(game, new Condition.Any([no, yes])));
        Assert.False(Holds(game, new Condition.Any([no, no])));
        Assert.False(Holds(game, new Condition.Negation(yes)));
        Assert.True(Holds(game, new Condition.Negation(no)));
        Assert.True(Holds(game, new Condition.Negation(new Condition.Negation(yes))));
    }

    [Fact]
    public void Combinators_nest()
    {
        var game = WithMemory(m => m.WithFlag("f").WithSeen("c")) with { Party = ["ann"] };
        Condition nested = new Condition.All([
            new Condition.Flag("f"),
            new Condition.Any([new Condition.InParty("bo"), new Condition.Negation(new Condition.All([new Condition.Seen("c"), new Condition.Flag("g")]))]),
        ]);

        Assert.True(Holds(game, nested));
        Assert.False(Holds(game, new Condition.Negation(nested)));
        Assert.False(Holds(Game(), nested));
    }

    [Fact]
    public void A_list_means_all_and_an_empty_or_missing_list_holds()
    {
        var game = WithMemory(m => m.WithFlag("f"));

        Assert.True(Holds(game, new Condition.Flag("f"), new Condition.Negation(new Condition.Flag("g"))));
        Assert.False(Holds(game, new Condition.Flag("f"), new Condition.Flag("g")));
        Assert.True(Holds(game));
        Assert.True(Eval.Holds(Content, game, default));
        Assert.True(Eval.Holds(Content, game, ImmutableArray<Condition>.Empty));
        Assert.True(Holds(game, new Condition.All([])));
        Assert.False(Holds(game, new Condition.Any([])));
    }

    [Fact]
    public void Explain_marks_Met_on_every_node_and_agrees_with_Holds()
    {
        var game = WithMemory(m => m.WithFlag("f"));
        ImmutableArray<Condition> list = [
            new Condition.Flag("f"),
            new Condition.Any([new Condition.Flag("g"), new Condition.Negation(new Condition.Flag("f"))]),
        ];

        var root = Assert.IsType<RequirementView.All>(Eval.Explain(Content, game, list));
        Assert.False(root.Met);
        Assert.True(root.Children[0].Met);
        var any = Assert.IsType<RequirementView.Any>(root.Children[1]);
        Assert.False(any.Met);
        Assert.False(any.Children[0].Met);
        var not = Assert.IsType<RequirementView.Negation>(any.Children[1]);
        Assert.False(not.Met);
        Assert.True(not.Child.Met);
        Assert.Equal(Eval.Holds(Content, game, list), root.Met);

        var empty = Eval.Explain(Content, game, []);
        Assert.True(empty.Met);
        Assert.Empty(Assert.IsType<RequirementView.All>(empty).Children);
    }

    [Fact]
    public void Explain_carries_current_values_names_and_reasons()
    {
        var game = Game();
        var logic = game.Player.Personality[Trait.Logic];
        ImmutableArray<Condition> list = [
            new Condition.TraitRange(Trait.Logic, logic + 5, null),
            new Condition.RelationshipRange("ann", RelationshipMeasure.Trust, null, -5),
            new Condition.InParty("ann"),
            new Condition.LineupReady("brawl"),
            new Condition.Flag("f"),
            new Condition.Chose("c", "pick"),
            new Condition.Seen("c"),
        ];

        var children = Assert.IsType<RequirementView.All>(Eval.Explain(Content, game, list)).Children;

        var trait = Assert.IsType<RequirementView.TraitRange>(children[0]);
        Assert.Equal(logic, trait.Current);
        Assert.Equal(logic + 5, trait.AtLeast);
        Assert.False(trait.Met);

        var relationship = Assert.IsType<RequirementView.RelationshipRange>(children[1]);
        Assert.Equal("Ann", relationship.CharacterName);
        Assert.Equal(RelationshipMeasure.Trust, relationship.Measure);
        Assert.Equal(-5, relationship.Current);
        Assert.Equal(-5, relationship.AtMost);
        Assert.True(relationship.Met);

        var party = Assert.IsType<RequirementView.InParty>(children[2]);
        Assert.Equal("Ann", party.CharacterName);
        Assert.False(party.Met);

        var lineup = Assert.IsType<RequirementView.LineupReady>(children[3]);
        Assert.Equal("brawl", lineup.FightId);
        Assert.False(lineup.Met);
        Assert.False(string.IsNullOrEmpty(lineup.Reason));

        Assert.Equal("f", Assert.IsType<RequirementView.Flag>(children[4]).FlagId);
        Assert.False(children[4].Met);
        var chose = Assert.IsType<RequirementView.Chose>(children[5]);
        Assert.Equal(("c", "pick"), (chose.ConversationId, chose.ChoiceId));
        Assert.Equal("c", Assert.IsType<RequirementView.Seen>(children[6]).ConversationId);

        var ready = Game() with { Party = ["ann"], Lineup = ["ann"] };
        var readyView = Assert.IsType<RequirementView.LineupReady>(
            Assert.Single(Assert.IsType<RequirementView.All>(Eval.Explain(Content, ready, [new Condition.LineupReady(null)])).Children));
        Assert.True(readyView.Met);
        Assert.Null(readyView.Reason);
    }

    [Fact]
    public void Explain_names_an_unknown_character_by_its_id_and_reads_zero()
    {
        var root = Assert.IsType<RequirementView.All>(
            Eval.Explain(Content, Game(), [new Condition.RelationshipRange("zed", RelationshipMeasure.Affection, 1, null), new Condition.InParty("zed")]));

        var relationship = Assert.IsType<RequirementView.RelationshipRange>(root.Children[0]);
        Assert.Equal("zed", relationship.CharacterName);
        Assert.Equal(0, relationship.Current);
        Assert.False(relationship.Met);
        Assert.Equal("zed", Assert.IsType<RequirementView.InParty>(root.Children[1]).CharacterName);
    }
}
