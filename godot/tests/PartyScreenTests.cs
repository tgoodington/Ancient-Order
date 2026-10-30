using AncientOrder.Ui;
using Godot;

namespace AncientOrder.Tests;

/// <summary>Choices shape the party (ES-8): the real party screen driven through the flow. Readouts, cards in join order, lineup toggles, counts and reasons, refusals, and both display switches.</summary>
public class PartyScreenTests
{
    private static readonly string[] JoinOrder = ["Bo", "Cy", "Ann"];

    private static async Task<(StartedGame Game, PartyScreen Screen)> OpenParty(TestContext t, Dictionary<string, string> pack)
    {
        var game = await t.StartGame(pack);
        game.Flow.StartNewGame();
        await t.NextFrame();
        game.Flow.OpenParty();
        await t.NextFrame();
        Assert.True(game.Flow.IsPartyOpen, "party screen did not open");
        var screen = (PartyScreen)Assert.NotNull(game.Flow.PartyScreen, "no party screen");
        Assert.True(screen.Visible, "party screen hidden");
        return (game, screen);
    }

    private static List<PanelContainer> Cards(PartyScreen screen) => screen.GetNode<VBoxContainer>("%Cards").GetChildren().OfType<PanelContainer>().ToList();

    private static string NameOf(PanelContainer card) => card.GetNode<Label>("%Name").Text;

    private static PanelContainer Card(PartyScreen screen, string name) => Cards(screen).First(c => NameOf(c) == name);

    private static CheckButton Toggle(PartyScreen screen, string name) => Card(screen, name).GetNode<CheckButton>("%Toggle");

    private static string[] Lineup(StartedGame game) => game.Session!.Snapshot.Party!.Lineup.Select(m => m.Id).ToArray();

    private static async Task Flip(TestContext t, PartyScreen screen, string name, bool on)
    {
        Toggle(screen, name).ButtonPressed = on;
        await t.NextFrame();
    }

    private static string J(IEnumerable<string> items) => string.Join("|", items);

    private static string AllText(Node root)
    {
        var parts = new List<string>();
        foreach (var node in root.FindChildren("*", "", true, false).Prepend(root))
        {
            if (node is Label label && label.IsVisibleInTree())
            {
                parts.Add(label.Text);
            }
            else if (node is Button button && button.IsVisibleInTree())
            {
                parts.Add(button.Text);
            }
        }

        return string.Join("\n", parts);
    }

    private static List<string> LabelTexts(Node root) =>
        root.FindChildren("*", "Label", true, false).Cast<Label>().Select(l => l.Text).ToList();

    [GodotTest]
    public async Task The_readout_shows_the_six_trait_values_and_the_member_cards_show_in_join_order(TestContext t)
    {
        var (_, screen) = await OpenParty(t, PartyScreenPacks.Standard());

        Assert.Equal("Rowan", screen.GetNode<Label>("%PlayerName").Text, "player name");
        var rows = screen.GetNode<VBoxContainer>("%PlayerTraits").GetChildren();
        Assert.Equal(6, rows.Count, "player trait rows");
        var player = string.Join(" ", rows.SelectMany(r => LabelTexts(r)));
        foreach (var trait in new[] { "Patience", "Empathy", "Cunning", "Logic", "Kindness", "Charisma" })
        {
            Assert.Contains(trait, player, "player trait name");
        }

        Assert.Contains("17%", player, "a trait value");
        Assert.Contains("17%", player, "a trait value");

        Assert.Equal(J(JoinOrder), J(Cards(screen).Select(NameOf)), "cards in join order");
        foreach (var card in Cards(screen))
        {
            Assert.True(card.GetNode<VBoxContainer>("%Readout").Visible, "member readout hidden with readouts on");
            Assert.Equal(6, card.GetNode<GridContainer>("%Traits").GetChildCount(), "member trait cells");
        }
    }

    [GodotTest]
    public async Task Toggling_a_member_on_and_off_changes_the_sessions_lineup(TestContext t)
    {
        var (game, screen) = await OpenParty(t, PartyScreenPacks.Standard());
        Assert.Equal("", J(Lineup(game)), "starting lineup");
        Assert.False(Toggle(screen, "Ann").ButtonPressed, "toggle starts off");

        await Flip(t, screen, "Ann", true);
        Assert.Equal(J(new[] { "ann" }), J(Lineup(game)), "lineup after Ann on");
        Assert.True(Toggle(screen, "Ann").ButtonPressed, "Ann's toggle shows on");

        await Flip(t, screen, "Bo", true);
        Assert.Equal(J(new[] { "ann", "bo" }), J(Lineup(game)), "lineup in pick order");

        await Flip(t, screen, "Ann", false);
        Assert.Equal(J(new[] { "bo" }), J(Lineup(game)), "lineup after Ann off");
        Assert.False(Toggle(screen, "Ann").ButtonPressed, "Ann's toggle shows off");
        Assert.True(Toggle(screen, "Bo").ButtonPressed, "Bo's toggle stays on");
    }

    [GodotTest]
    public async Task A_member_who_cannot_fight_has_a_disabled_toggle_and_says_so(TestContext t)
    {
        var (_, screen) = await OpenParty(t, PartyScreenPacks.Standard());

        Assert.True(Toggle(screen, "Cy").Disabled, "non-fighter toggle enabled");
        Assert.True(Card(screen, "Cy").GetNode<Label>("%Status").Visible, "no 'does not fight' note");
        Assert.False(Toggle(screen, "Ann").Disabled, "fighter toggle disabled");
        Assert.False(Card(screen, "Ann").GetNode<Label>("%Status").Visible, "fighter shows the note");
    }

    [GodotTest]
    public async Task The_lineup_count_and_the_readiness_reason_follow_the_picks(TestContext t)
    {
        var (_, screen) = await OpenParty(t, PartyScreenPacks.Standard());
        var count = screen.GetNode<Label>("%LineupCount");
        var reason = screen.GetNode<Label>("%Reason");
        var empty = screen.GetNode<Label>("%LineupEmpty");
        var names = screen.GetNode<VBoxContainer>("%LineupNames");

        Assert.Equal("0 of 2", count.Text, "count with nobody picked");
        Assert.True(empty.Visible, "empty lineup line hidden");
        Assert.True(reason.Visible, "reason hidden below the minimum");
        Assert.Equal("Pick at least 2 fighters", reason.Text, "reason text");

        await Flip(t, screen, "Ann", true);
        Assert.Equal("1 of 2", count.Text, "count with one picked");
        Assert.False(empty.Visible, "empty line still shown");
        Assert.True(reason.Visible, "reason hidden with one picked");
        Assert.Equal(J(new[] { "1.  Ann" }), J(LabelTexts(names)), "lineup names");

        await Flip(t, screen, "Bo", true);
        Assert.Equal("2 of 2", count.Text, "count at the limit");
        Assert.False(reason.Visible, "reason shown when ready");
        Assert.Equal(J(new[] { "1.  Ann", "2.  Bo" }), J(LabelTexts(names)), "lineup names in pick order");
    }

    [GodotTest]
    public async Task An_empty_party_shows_its_empty_line_and_no_cards(TestContext t)
    {
        var (_, screen) = await OpenParty(t, PartyScreenPacks.EmptyParty());

        Assert.True(screen.GetNode<Label>("%PartyEmpty").Visible, "empty party line hidden");
        Assert.Equal(0, Cards(screen).Count, "cards for an empty party");
        Assert.Equal("0 of 2", screen.GetNode<Label>("%LineupCount").Text, "count");
        Assert.Equal("Pick at least 2 fighters", screen.GetNode<Label>("%Reason").Text, "reason");
    }

    [GodotTest]
    public async Task A_party_with_members_hides_the_empty_line(TestContext t)
    {
        var (_, screen) = await OpenParty(t, PartyScreenPacks.Standard());

        Assert.False(screen.GetNode<Label>("%PartyEmpty").Visible, "empty line shown with members");
    }

    [GodotTest]
    public async Task A_refused_toggle_snaps_back_and_shows_the_banner(TestContext t)
    {
        var game = await t.StartGame(PartyScreenPacks.Standard());
        game.Flow.StartNewGame();
        await t.NextFrame();
        Assert.True(game.Session!.StartConversation(PartyScreenPacks.ConversationId).IsOk, "conversation did not start");
        await t.NextFrame();
        game.Flow.OpenParty();
        await t.NextFrame();
        var screen = (PartyScreen)game.Flow.PartyScreen!;
        var banner = game.Flow.GetNode<ErrorBanner>("TopLayer/ErrorBanner");
        Assert.False(banner.Visible, "banner shown before any refusal");

        Toggle(screen, "Ann").ButtonPressed = true;
        await t.WaitUntil(() => banner.Visible, "the refusal banner");

        Assert.Equal("Cannot change team during a conversation", banner.GetNode<Label>("Message").Text, "banner message");
        Assert.Equal("", J(Lineup(game)), "lineup changed during a conversation");
        Assert.False(Toggle(screen, "Ann").ButtonPressed, "refused toggle did not snap back");
    }

    [GodotTest]
    public async Task Toggling_a_member_on_when_the_lineup_is_full_says_so_and_changes_nothing(TestContext t)
    {
        var (game, screen) = await OpenParty(t, PartyScreenPacks.Standard(lineupMin: 1, lineupMax: 1));
        var banner = game.Flow.GetNode<ErrorBanner>("TopLayer/ErrorBanner");
        await Flip(t, screen, "Ann", true);
        Assert.False(Toggle(screen, "Bo").Disabled, "toggle disabled with a full lineup");

        Toggle(screen, "Bo").ButtonPressed = true;
        await t.WaitUntil(() => banner.Visible, "the lineup full banner");

        Assert.Equal("Lineup full", banner.GetNode<Label>("Message").Text, "banner message");
        Assert.Equal(J(new[] { "ann" }), J(Lineup(game)), "lineup changed when full");
        Assert.False(Toggle(screen, "Bo").ButtonPressed, "refused toggle did not snap back");
        Assert.True(Toggle(screen, "Ann").ButtonPressed, "Ann's toggle changed");
    }

    [GodotTest]
    public async Task Turning_one_off_and_another_on_swaps_them_when_the_lineup_is_full(TestContext t)
    {
        var (game, screen) = await OpenParty(t, PartyScreenPacks.Standard(lineupMin: 1, lineupMax: 1));
        await Flip(t, screen, "Ann", true);

        await Flip(t, screen, "Ann", false);
        await Flip(t, screen, "Bo", true);

        Assert.Equal(J(new[] { "bo" }), J(Lineup(game)), "lineup after the swap");
        Assert.True(Toggle(screen, "Bo").ButtonPressed, "Bo's toggle shows on");
        Assert.False(Toggle(screen, "Ann").ButtonPressed, "Ann's toggle shows off");
    }

    [GodotTest]
    public async Task With_both_switches_off_there_are_no_member_readouts_or_bonus_numbers_but_the_feel_line_shows(TestContext t)
    {
        var (_, screen) = await OpenParty(t, PartyScreenPacks.Standard(readouts: false, numbers: false));
        await Flip(t, screen, "Ann", true);

        foreach (var card in Cards(screen))
        {
            Assert.False(card.GetNode<VBoxContainer>("%Readout").Visible, "member readout shown with readouts off");
            Assert.Equal(0, card.GetNode<GridContainer>("%Traits").GetChildCount(), "member trait cells with readouts off");
            Assert.False(card.GetNode<Label>("%Combat").Visible, "member combat line shown with readouts off");
            Assert.False(card.FindChildren("*", "ProgressBar", true, false).Any(), "member bar shown with readouts off");
        }

        Assert.Equal(J(JoinOrder), J(Cards(screen).Select(NameOf)), "cards still show names");
        Assert.True(screen.GetNode<VBoxContainer>("%Together").Visible, "together section hidden though a bonus is active");
        var together = LabelTexts(screen.GetNode<VBoxContainer>("%TogetherList"));
        Assert.Equal(J(new[] { PartyScreenPacks.Feel }), J(together), "only the feel line shows");
        Assert.NoDigits(string.Join(" ", together), "together text");
        Assert.True(screen.GetNode<VBoxContainer>("%PlayerTraits").GetChildCount() == 6, "the player's own readout is unaffected");
    }

    [GodotTest]
    public async Task With_both_switches_on_member_readouts_and_bonus_numbers_show_beside_the_feel_line(TestContext t)
    {
        var (_, screen) = await OpenParty(t, PartyScreenPacks.Standard(readouts: true, numbers: true));
        await Flip(t, screen, "Ann", true);

        var combat = Card(screen, "Ann").GetNode<Label>("%Combat");
        Assert.True(combat.Visible, "member combat line hidden with readouts on");
        Assert.Contains("Power 80", combat.Text, "member combat values");
        var together = LabelTexts(screen.GetNode<VBoxContainer>("%TogetherList"));
        Assert.Equal(2, together.Count, "feel line plus bonus numbers");
        Assert.Contains(PartyScreenPacks.Feel, string.Join("\n", together), "feel line");
        Assert.Contains("Any   x1.10 Power", string.Join("\n", together), "bonus numbers");
    }

    [GodotTest]
    public async Task Only_the_synergy_numbers_switch_hides_the_bonus_numbers(TestContext t)
    {
        var (_, screen) = await OpenParty(t, PartyScreenPacks.Standard(readouts: true, numbers: false));
        await Flip(t, screen, "Ann", true);

        Assert.True(Card(screen, "Ann").GetNode<VBoxContainer>("%Readout").Visible, "readouts on but hidden");
        Assert.Equal(J(new[] { PartyScreenPacks.Feel }), J(LabelTexts(screen.GetNode<VBoxContainer>("%TogetherList"))), "only the feel line shows");
    }

    [GodotTest]
    public async Task Only_the_readouts_switch_hides_the_member_readouts(TestContext t)
    {
        var (_, screen) = await OpenParty(t, PartyScreenPacks.Standard(readouts: false, numbers: true));
        await Flip(t, screen, "Ann", true);

        Assert.False(Card(screen, "Ann").GetNode<VBoxContainer>("%Readout").Visible, "readout shown with readouts off");
        Assert.Contains("x1.10", AllText(screen.GetNode<VBoxContainer>("%TogetherList")), "bonus numbers hidden though switched on");
    }

    [GodotTest]
    public async Task Trait_bars_never_show_a_percentage(TestContext t)
    {
        var (_, screen) = await OpenParty(t, PartyScreenPacks.Standard());

        var bars = screen.FindChildren("*", "ProgressBar", true, false).Cast<ProgressBar>().ToList();
        Assert.Equal(6 + 3 * 6, bars.Count, "bars for the player and three members");
        Assert.True(bars.All(b => !b.ShowPercentage), "a progress bar shows its own percentage");
    }

    [GodotTest]
    public async Task No_affection_or_trust_value_appears_on_the_screen(TestContext t)
    {
        var (_, screen) = await OpenParty(t, PartyScreenPacks.Standard());
        await Flip(t, screen, "Ann", true);
        await Flip(t, screen, "Bo", true);

        var text = AllText(screen);
        Assert.False(text.Contains(PartyScreenPacks.AffectionText, StringComparison.Ordinal), "affection value on screen");
        Assert.False(text.Contains(PartyScreenPacks.TrustText, StringComparison.Ordinal), "trust value on screen");
        Assert.False(text.Contains("Affection", StringComparison.OrdinalIgnoreCase), "the word affection on screen");
        Assert.False(text.Contains("Trust", StringComparison.OrdinalIgnoreCase), "the word trust on screen");
    }

    [GodotTest]
    public async Task Only_the_player_and_party_members_appear(TestContext t)
    {
        var (_, screen) = await OpenParty(t, PartyScreenPacks.Standard());

        Assert.False(AllText(screen).Contains("Dee", StringComparison.Ordinal), "a character outside the party appears");
    }
}
