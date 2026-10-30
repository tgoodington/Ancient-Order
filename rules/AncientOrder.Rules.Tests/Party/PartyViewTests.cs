using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Party;
using AncientOrder.Rules.State;
using AncientOrder.Rules.Tests.Fixtures;

namespace AncientOrder.Rules.Tests.Party;

// T32: what PartyView carries. PartyTests already covers the view through the session (readouts present, one switch-off case); this builds
// the view directly and covers each switch on and off, join order, and who may appear.
public class PartyViewContents
{
    private const string ElenaId = TestCast.ElenaId;
    private const string LarsId = TestCast.LarsId;
    private const string KadeId = TestCast.KadeId;
    private const long Later = TestContent.Now + 5_000;
    private const string Feel = "You move as one.";

    private static readonly SynergySettings Bonus = new(
        [new SynergyBonusDefinition("Any", SynergyRule.Coverage, 1, CombatStat.Power, StatStep.Multiply, 1.1, Feel)],
        SynergyResolution.AllThatQualify);

    private static GameContent ContentWith(bool readouts = true, bool numbers = true, LineupLimits? limits = null, string[]? party = null, CombatProfile? playerCombat = null)
    {
        var settings = new GameSettings("Kael", limits ?? new LineupLimits(1, 2), null, playerCombat)
        {
            StartingParty = [.. party ?? [ElenaId, LarsId, KadeId]],
            Display = new DisplaySettings(readouts, numbers),
        };
        return TestContent.Create(settings, synergy: Bonus);
    }

    private static PartyView ViewOf(GameContent content, params string[] lineup)
    {
        var game = TestContent.NewGame(content);
        if (lineup.Length > 0)
        {
            game = LineupRules.Set(content, game, [.. lineup], Later).Value;
        }

        return PartyView.From(content, game);
    }

    [Fact]
    public void Members_come_in_join_order_with_names_and_flags()
    {
        var content = TestContent.Create(
            TestContent.Settings with { StartingParty = [KadeId, ElenaId, LarsId], Lineup = new LineupLimits(1, 2) },
            [TestCast.Elena, TestCast.Lars with { Combat = null }, TestCast.Kade]);
        var view = ViewOf(content, KadeId);

        Assert.Equal([KadeId, ElenaId, LarsId], view.Members.Select(m => m.Id));
        Assert.Equal(["Kade", "Elena", "Lars"], view.Members.Select(m => m.Name));
        Assert.Equal([true, true, false], view.Members.Select(m => m.CanFight));
        Assert.Equal([true, false, false], view.Members.Select(m => m.InLineup));
    }

    [Fact]
    public void The_lineup_is_in_pick_order_and_the_limits_and_readiness_come_from_content()
    {
        var content = ContentWith(limits: new LineupLimits(2, 3));
        var none = ViewOf(content);
        Assert.Empty(none.Lineup);
        Assert.Equal(new LineupLimits(2, 3), none.Limits);
        Assert.Equal(new LineupReadiness(false, "Pick at least 2 fighters"), none.Readiness);

        var picked = ViewOf(content, KadeId, ElenaId);
        Assert.Equal([KadeId, ElenaId], picked.Lineup.Select(l => l.Id));
        Assert.Equal(["Kade", "Elena"], picked.Lineup.Select(l => l.Name));
        Assert.Equal(new LineupReadiness(true, null), picked.Readiness);
    }

    [Fact]
    public void An_empty_party_gives_an_empty_view_that_is_not_ready()
    {
        var view = ViewOf(ContentWith(party: []));
        Assert.Empty(view.Members);
        Assert.Empty(view.Lineup);
        Assert.Empty(view.Synergy);
        Assert.False(view.Readiness.Ready);
        Assert.Equal("Kael", view.Player.Name);
    }

    [Fact]
    public void Only_the_player_and_party_members_appear()
    {
        var view = ViewOf(ContentWith(party: [ElenaId]));
        Assert.Equal([ElenaId], view.Members.Select(m => m.Id));
        Assert.Equal("Kael", view.Player.Name);
    }

    [Fact]
    public void A_party_id_missing_from_the_catalog_is_skipped_not_thrown()
    {
        var content = ContentWith(party: [ElenaId]);
        var game = TestContent.NewGame(content) with { Party = [ElenaId, "ghost"], Lineup = [ElenaId, "ghost"] };
        var view = PartyView.From(content, game);
        Assert.Equal([ElenaId], view.Members.Select(m => m.Id));
        Assert.Equal([ElenaId], view.Lineup.Select(l => l.Id));
    }

    [Fact]
    public void The_player_readout_carries_personality_and_combat_only_when_the_player_fights()
    {
        var content = ContentWith();
        var plain = ViewOf(content);
        Assert.Null(plain.Player.Combat);
        Assert.Equal(TestContent.NewGame(content).Player.Personality, plain.Player.Personality);

        var fighter = new CombatProfile(3, 100, 80, 80, ElementalPath.Air);
        Assert.Equal(fighter, ViewOf(ContentWith(playerCombat: fighter)).Player.Combat);
    }

    [Fact]
    public void With_readouts_on_each_member_has_their_personality_and_combat()
    {
        var view = ViewOf(ContentWith(readouts: true));
        Assert.True(view.ShowMemberReadouts);
        var elena = view.Members[0].Readout!;
        Assert.Equal(TestCast.Elena.Personality, elena.Personality);
        Assert.Equal(TestCast.Elena.Combat, elena.Combat);
    }

    [Fact]
    public void With_readouts_off_no_member_has_a_readout_but_names_and_flags_remain()
    {
        var view = ViewOf(ContentWith(readouts: false), ElenaId);
        Assert.False(view.ShowMemberReadouts);
        Assert.All(view.Members, m => Assert.Null(m.Readout));
        Assert.Equal([ElenaId, LarsId, KadeId], view.Members.Select(m => m.Id));
        Assert.True(view.Members[0].InLineup);
        Assert.True(view.Members[0].CanFight);
    }

    [Fact]
    public void With_synergy_numbers_on_a_bonus_has_its_name_stat_step_value_and_feel()
    {
        var view = ViewOf(ContentWith(numbers: true), ElenaId, LarsId);
        Assert.True(view.ShowSynergyNumbers);
        Assert.Equal(new PartySynergyView(Feel, "Any", CombatStat.Power, StatStep.Multiply, 1.1), Assert.Single(view.Synergy));
    }

    [Fact]
    public void With_synergy_numbers_off_only_the_feel_line_remains()
    {
        var view = ViewOf(ContentWith(numbers: false), ElenaId, LarsId);
        Assert.False(view.ShowSynergyNumbers);
        Assert.Equal(new PartySynergyView(Feel, null, null, null, null), Assert.Single(view.Synergy));
    }

    [Fact]
    public void The_two_switches_work_independently()
    {
        var readoutsOnly = ViewOf(ContentWith(readouts: true, numbers: false), ElenaId);
        Assert.All(readoutsOnly.Members, m => Assert.NotNull(m.Readout));
        Assert.Null(Assert.Single(readoutsOnly.Synergy).Name);

        var numbersOnly = ViewOf(ContentWith(readouts: false, numbers: true), ElenaId);
        Assert.All(numbersOnly.Members, m => Assert.Null(m.Readout));
        Assert.Equal("Any", Assert.Single(numbersOnly.Synergy).Name);
    }

    [Fact]
    public void The_view_never_carries_affection_or_trust()
    {
        var types = new[] { typeof(PartyView), typeof(PlayerReadout), typeof(MemberReadout), typeof(PartyMemberView), typeof(LineupMemberView), typeof(PartySynergyView) };
        var names = types.SelectMany(t => t.GetProperties()).Select(p => p.Name);
        Assert.DoesNotContain(names, n => n.Contains("Affection", StringComparison.OrdinalIgnoreCase) || n.Contains("Trust", StringComparison.OrdinalIgnoreCase));
    }
}
