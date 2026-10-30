using AncientOrder.Rules.Common;
using AncientOrder.Rules.Content.Packs;
using AncientOrder.Rules.Effects;
using AncientOrder.Rules.Party;
using AncientOrder.Rules.State;
using AncientOrder.Rules.Tests.Fixtures;
using AncientOrder.Rules.Tests.Support;

namespace AncientOrder.Rules.Tests.Party;

public class PartyTests
{
    private const long Later = TestContent.Now + 5_000;
    private const string Pack = """{ "format": "ancient-order-content", "version": 1, "id": "t", "name": "Test", "standIn": false, "files": ["a.json"] }""";
    private const string Fighter = """, "combat": { "rank": 3, "stamina": 100, "power": 10, "speed": 10, "elementalPath": "fire" }""";
    private const string StartAnn = """, "startingParty": ["ann"]""";
    private const string StartAnnBo = """, "startingParty": ["ann", "bo"]""";
    private const string AnyBonus = """
        , "synergy": { "resolution": "allThatQualify", "bonuses": [ { "name": "Any", "rule": "coverage", "threshold": 1, "stat": "power", "step": "multiply", "value": 1.1, "feel": "You move as one." } ] }
        """;

    private static string Person(string id, string combat) =>
        $$"""{ "id": "{{id}}", "name": "{{id.ToUpperInvariant()}}", "archetypeId": "x", "personality": { "patience": 20, "empathy": 20, "cunning": 10, "logic": 15, "kindness": 20, "charisma": 15 }, "startingAffection": 7, "startingTrust": 9 {{combat}} }""";

    private static Result<GameContent> Load(string settingsExtra, string synergy = "", string conversations = "")
    {
        var file = $$"""
            {
              "settings": { "playerName": "Kael", "lineup": { "min": 1, "max": 2 } {{settingsExtra}} },
              "archetypes": [{ "id": "x" }],
              "characters": [ {{Person("ann", Fighter)}}, {{Person("bo", string.Empty)}}, {{Person("cy", Fighter)}} ]
              {{synergy}} {{conversations}}
            }
            """;
        return ContentPackLoader.Load(new MemoryContentSource([KeyValuePair.Create("pack.json", Pack), KeyValuePair.Create("a.json", file)]));
    }

    private static GameContent Content(string settingsExtra = "", string synergy = "", string conversations = "")
    {
        var loaded = Load(settingsExtra, synergy, conversations);
        Assert.True(loaded.IsOk, loaded.IsOk ? string.Empty : string.Join("\n", loaded.Error.Details));
        return loaded.Value;
    }

    [Fact]
    public void A_new_game_starts_with_the_content_starting_party_in_order_or_empty()
    {
        Assert.Empty(TestContent.NewGame(Content()).Party);
        Assert.Equal(["cy", "ann"], TestContent.NewGame(Content(""", "startingParty": ["cy", "ann"]""")).Party);
    }

    [Fact]
    public void Starting_party_ids_must_exist_and_be_unique()
    {
        var unknown = Load(""", "startingParty": ["zed"]""");
        Assert.False(unknown.IsOk);
        Assert.Contains("a.json:/settings/startingParty/0", unknown.Error.Details.Select(d => d.Path));

        var twice = Load(""", "startingParty": ["ann", "ann"]""");
        Assert.False(twice.IsOk);
        Assert.Contains("a.json:/settings/startingParty/1", twice.Error.Details.Select(d => d.Path));
    }

    [Fact]
    public void Lineup_limits_need_zero_le_min_le_max()
    {
        var content = Content();
        Assert.False(GameContent.Create(content.Settings with { Lineup = new LineupLimits(3, 2) }, content.Characters.Definitions, content.Archetypes.Definitions).IsOk);
        Assert.False(GameContent.Create(content.Settings with { Lineup = new LineupLimits(-1, 2) }, content.Characters.Definitions, content.Archetypes.Definitions).IsOk);
        Assert.True(GameContent.Create(content.Settings with { Lineup = new LineupLimits(0, 0) }, content.Characters.Definitions, content.Archetypes.Definitions).IsOk);
    }

    [Fact]
    public void Join_adds_in_order_and_twice_changes_nothing_and_leave_also_leaves_the_lineup()
    {
        var content = Content();
        var game = TestContent.NewGame(content);
        game = PartyRules.Join(content, game, "cy", Later).Value;
        game = PartyRules.Join(content, game, "ann", Later).Value;
        Assert.Equal(["cy", "ann"], game.Party);
        Assert.Same(game, PartyRules.Join(content, game, "cy", Later + 1).Value);

        game = LineupRules.Set(content, game, ["cy", "ann"], Later).Value;
        var left = PartyRules.Leave(content, game, "cy", Later + 2).Value;
        Assert.Equal(["ann"], left.Party);
        Assert.Equal(["ann"], left.Lineup);
        Assert.Same(left, PartyRules.Leave(content, left, "cy", Later + 3).Value);
        Assert.False(PartyRules.Join(content, game, "nobody", Later).IsOk);
    }

    [Fact]
    public void The_lineup_needs_party_members_who_can_fight()
    {
        var content = Content(StartAnnBo);
        var game = TestContent.NewGame(content);
        Assert.Equal("cy is not in the party", LineupRules.Set(content, game, ["cy"], Later).Error.Message);
        Assert.Equal("bo has no combat stats", LineupRules.Set(content, game, ["bo"], Later).Error.Message);
        Assert.True(LineupRules.Set(content, game, ["ann"], Later).IsOk);
    }

    [Fact]
    public void Join_and_leave_effects_report_who_changed_and_skip_no_ops()
    {
        var content = Content(StartAnn);
        var game = TestContent.NewGame(content);
        var applied = EffectRules.Apply(content, game, [new Effect.Join("ann"), new Effect.Join("cy"), new Effect.Leave("ann"), new Effect.Leave("bo")], Later).Value;
        Assert.Equal(["cy"], applied.Joined);
        Assert.Equal(["ann"], applied.Left);
        Assert.Equal(["cy"], applied.Game.Party);

        Assert.False(EffectRules.Apply(content, game, [new Effect.Join("cy"), new Effect.Join("zed")], Later).IsOk);
    }

    [Fact]
    public void A_choice_can_join_and_leave_and_the_report_says_so()
    {
        const string talk = """
            , "conversations": [ { "id": "c", "title": "T", "participants": [], "steps": [
              { "id": "s", "lines": [], "choices": [
                { "id": "swap", "text": "Swap", "effects": [ { "type": "join", "character": "cy" }, { "type": "leave", "character": "ann" } ] } ] } ] } ]
            """;
        var session = TestSessions.Create(Content(StartAnn, conversations: talk));
        session.NewGame();
        session.StartConversation("c");
        var report = session.Choose("swap").Value;
        Assert.Equal(["cy"], report.Joined);
        Assert.Equal(["ann"], report.Left);
        Assert.Equal(["cy"], session.Snapshot.Game!.Party);
        Assert.Equal(["cy"], session.Snapshot.Party!.Members.Select(m => m.Id));

        var bad = talk.Replace("\"cy\"", "\"zed\"", StringComparison.Ordinal);
        Assert.False(Load(StartAnn, conversations: bad).IsOk);
    }

    [Fact]
    public void Lineup_changes_are_refused_during_a_conversation_through_the_session()
    {
        const string talk = """
            , "conversations": [ { "id": "c", "title": "T", "participants": [], "steps": [ { "id": "s", "lines": [], "choices": [ { "id": "x", "text": "X" } ] } ] } ]
            """;
        var session = TestSessions.Create(Content(StartAnn, conversations: talk));
        session.NewGame();
        session.StartConversation("c");
        SessionAssert.Refused(session.SetLineup(["ann"]), ErrorCode.TEAM_COMPOSITION_INVALID);
    }

    [Fact]
    public void The_snapshot_carries_the_party_view()
    {
        var session = TestSessions.Create(Content(StartAnnBo, AnyBonus));
        Assert.Null(session.Snapshot.Party);
        session.NewGame();

        var view = session.Snapshot.Party!;
        Assert.Equal("Kael", view.Player.Name);
        Assert.Null(view.Player.Combat);
        Assert.Equal(["ann", "bo"], view.Members.Select(m => m.Id));
        Assert.Equal([true, false], view.Members.Select(m => m.CanFight));
        Assert.All(view.Members, m => Assert.NotNull(m.Readout));
        Assert.Empty(view.Lineup);
        Assert.Equal(new LineupLimits(1, 2), view.Limits);
        Assert.Equal(new LineupReadiness(false, "Pick at least 1 fighter"), view.Readiness);
        Assert.Empty(view.Synergy);

        Assert.True(session.SetLineup(["ann"]).IsOk);
        view = session.Snapshot.Party!;
        Assert.Equal([true, false], view.Members.Select(m => m.InLineup));
        Assert.Equal(["ann"], view.Lineup.Select(l => l.Id));
        Assert.True(view.Readiness.Ready);
        var bonus = Assert.Single(view.Synergy);
        Assert.Equal("You move as one.", bonus.Feel);
        Assert.Equal("Any", bonus.Name);
        Assert.Equal(1.1, bonus.Value);
    }

    [Fact]
    public void Display_switches_hide_member_readouts_and_synergy_numbers_but_keep_the_feel_line()
    {
        const string hidden = """, "startingParty": ["ann"], "display": { "partyReadouts": false, "synergyNumbers": false }""";
        var session = TestSessions.Create(Content(hidden, AnyBonus));
        session.NewGame();
        session.SetLineup(["ann"]);

        var view = session.Snapshot.Party!;
        Assert.False(view.ShowMemberReadouts);
        Assert.False(view.ShowSynergyNumbers);
        Assert.All(view.Members, m => Assert.Null(m.Readout));
        Assert.Equal(new PartySynergyView("You move as one.", null, null, null, null), Assert.Single(view.Synergy));
    }
}
