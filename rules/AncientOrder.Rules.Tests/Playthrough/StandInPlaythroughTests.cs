using AncientOrder.Rules.Common;
using AncientOrder.Rules.Content.Check;
using AncientOrder.Rules.Content.Packs;
using AncientOrder.Rules.Session;
using AncientOrder.Rules.Tests.Support;
using AncientOrder.Rules.Town;
using Microsoft.Extensions.Time.Testing;

namespace AncientOrder.Rules.Tests.Playthrough;

// Plays the real stand-in pack (godot/content/standin) through the session only. The test is tied to the pack on purpose:
// a stand-in change that drops a mechanic fails here, and the route in StandInRoute.cs is updated with the pack.
public class StandInPlaythroughTests
{
    private static string PackFolder()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AncientOrder.sln")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("AncientOrder.sln not found above the test folder"), StandInRoute.Pack);
    }

    private static ContentCheckReport Check() => ContentCheck.Run(new FolderContentSource(PackFolder()));

    private static string Describe(IEnumerable<RuleErrorDetail> details) => string.Join("\n", details.Select(d => $"{d.Path}: {d.Message}"));

    [Fact(DisplayName = "the stand-in pack passes the content check")]
    public void Pack_passes_the_content_check()
    {
        var report = Check();
        Assert.True(report.Passed, Describe(report.Problems));
        Assert.NotNull(report.Content);
    }

    [Fact(DisplayName = "the stand-in pack's conversation text has no digit warnings")]
    public void Conversation_text_has_no_digit_warnings()
    {
        var report = Check();
        var digits = report.Warnings.Where(w => w.Message.Contains("digit", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.True(digits.Count == 0, Describe(digits));
    }

    [Fact(DisplayName = "the scripted route reaches every mechanic and ends at the hand-off")]
    public async Task Route_reaches_every_mechanic_and_the_hand_off()
    {
        var content = Check().Content!;
        var session = new GameSession(content, new GameSessionOptions
        {
            TimeProvider = new FakeTimeProvider(DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000)),
            IdSource = new FixedIdSource("fixed"),
            SeedSource = new FixedSeedSource(TestSessions.Seed),
        });
        var checklist = new MechanicsChecklist();

        Assert.True(session.NewGame().IsOk);
        checklist.ObserveGameStart(session.Snapshot);

        var number = 0;
        foreach (var step in StandInRoute.Steps)
        {
            number++;
            switch (step)
            {
                case RouteStep.Save save:
                    Assert.True((await session.SaveToSlot(save.Slot)).IsOk, $"step {number}: save failed");
                    break;
                case RouteStep.Load load:
                    Ok(await session.LoadFromSlot(load.Slot), number, "load");
                    checklist.ObserveLoad(session.Snapshot);
                    break;
                case RouteStep.Lineup lineup:
                    Ok(session.SetLineup(lineup.Ids), number, "set the lineup");
                    checklist.ObserveLineup(session.Snapshot);
                    break;
                case RouteStep.Walk walk:
                    var moved = Ok(session.MovePlayer(walk.X, walk.Z), number, $"walk to {walk.X},{walk.Z}");
                    var scene = Assert.IsType<TownEvent.ConversationStarted>(moved);
                    checklist.ObserveConversationStart(session.Snapshot, byWalkOver: true);
                    ReadToEnd(session, checklist, number, scene.ConversationId);
                    break;
                case RouteStep.Talk talk:
                    Ok(session.MovePlayer(talk.X, talk.Z), number, $"walk to {talk.Target}");
                    var opened = Ok(talk.IsSpot ? session.TriggerSpot(talk.Target) : session.TriggerPerson(talk.Target), number, $"trigger {talk.Target}");
                    if (opened is TownEvent.HandedOff)
                    {
                        checklist.ObserveAfterMove(session.Snapshot);
                        Assert.Empty(talk.Choices);
                        break;
                    }

                    var started = Assert.IsType<TownEvent.ConversationStarted>(opened);
                    checklist.ObserveConversationStart(session.Snapshot, byWalkOver: false);
                    foreach (var choice in talk.Choices)
                    {
                        var before = session.Snapshot;
                        checklist.ObserveBeforeChoice(before);
                        Ok(session.Choose(choice), number, $"choose {choice} in {started.ConversationId}");
                        checklist.ObserveChoice(before, choice, session.Snapshot);
                    }

                    ReadToEnd(session, checklist, number, started.ConversationId);
                    break;
                default:
                    throw new InvalidOperationException($"step {number}: unknown route step");
            }
        }

        var missing = checklist.Missing();
        Assert.True(missing.Count == 0, "Stand-in mechanics not reached: " + string.Join("; ", missing));

        var handOff = session.Snapshot.HandOff;
        Assert.NotNull(handOff);
        Assert.Equal(StandInRoute.Encounter, handOff.FightId);
        Assert.Equal([StandInRoute.CompanionA, StandInRoute.Recruit, StandInRoute.Merchant], handOff.Lineup.Select(m => m.Id));
        Assert.Null(session.Snapshot.Conversation);
    }

    [Fact(DisplayName = "an empty checklist reports every mechanic as missing, by name")]
    public void Empty_checklist_names_every_mechanic()
    {
        var missing = new MechanicsChecklist().Missing();
        Assert.Equal(19, MechanicsChecklist.All.Count);
        Assert.Equal(MechanicsChecklist.All, missing);
    }

    [Fact(DisplayName = "a partial route leaves the unreached mechanics listed by name")]
    public void Partial_route_lists_the_rest()
    {
        var session = new GameSession(Check().Content!, new GameSessionOptions
        {
            TimeProvider = new FakeTimeProvider(DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000)),
            IdSource = new FixedIdSource("fixed"),
            SeedSource = new FixedSeedSource(TestSessions.Seed),
        });
        var checklist = new MechanicsChecklist();
        Assert.True(session.NewGame().IsOk);
        checklist.ObserveGameStart(session.Snapshot);
        Ok(session.MovePlayer(20, 16), 1, "walk");
        Ok(session.TriggerPerson(StandInRoute.Elder), 1, "trigger");
        checklist.ObserveConversationStart(session.Snapshot, byWalkOver: false);
        var before = session.Snapshot;
        checklist.ObserveBeforeChoice(before);
        Ok(session.Choose("warm"), 1, "choose");
        checklist.ObserveChoice(before, "warm", session.Snapshot);

        var missing = checklist.Missing();
        Assert.DoesNotContain(MechanicsChecklist.Shift, missing);
        Assert.DoesNotContain(MechanicsChecklist.TwoArchetypes, missing);
        Assert.Contains(MechanicsChecklist.HandOff, missing);
        Assert.Contains(MechanicsChecklist.Departure, missing);
        Assert.Contains(MechanicsChecklist.LineupPicked, missing);
        Assert.Contains(MechanicsChecklist.WrittenOverride, missing);
        Assert.Contains(MechanicsChecklist.HiddenOutcome, missing);
    }

    private static T Ok<T>(Result<T> result, int step, string what)
    {
        Assert.True(result.IsOk, result.IsOk ? string.Empty : $"step {step}: could not {what}: {result.Error.Message}");
        return result.Value;
    }

    private static void ReadToEnd(GameSession session, MechanicsChecklist checklist, int step, string conversationId)
    {
        for (var guard = 0; session.Snapshot.Conversation is { } view; guard++)
        {
            Assert.True(view.IsContinue && guard < 20, $"step {step}: {view.Id} stopped at step {view.StepId} with choices to pick: {string.Join(", ", view.Choices.Select(c => c.Id))}");
            var before = session.Snapshot;
            Ok(session.Continue(), step, $"continue {view.Id}/{view.StepId}");
            checklist.ObserveChoice(before, null, session.Snapshot);
        }

        Assert.True(session.Snapshot.LastChoice is not null, $"step {step}: {conversationId} did not finish");
    }
}
