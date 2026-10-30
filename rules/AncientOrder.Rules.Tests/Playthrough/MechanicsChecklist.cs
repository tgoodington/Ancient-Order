using AncientOrder.Rules.Conversations;
using AncientOrder.Rules.Reactions;
using AncientOrder.Rules.Session;
using AncientOrder.Rules.Town;

namespace AncientOrder.Rules.Tests.Playthrough;

/// <summary>Collects evidence of each stand-in mechanic from the snapshots and choice reports a playthrough sees.</summary>
internal sealed class MechanicsChecklist
{
    public const string Shift = "a personality shift";
    public const string LockedFallback = "a locked choice seen and its fallback taken";
    public const string TwoArchetypes = "two archetypes reacting differently to one choice";
    public const string WrittenOverride = "a written reaction override";
    public const string Silence = "a silenced reaction";
    public const string TellStrengths = "tells of at least two strengths";
    public const string Demeanors = "people of at least two demeanors";
    public const string HiddenOutcome = "a hidden-outcome choice resolving by a hidden condition";
    public const string Memory = "a later line, choice, or destination changed by memory";
    public const string ConditionalOpening = "a conditional opening step";
    public const string ChainedScene = "a chained narrated scene";
    public const string WalkOverScene = "a walk-over spot starting a scene";
    public const string PersonAppears = "a person or spot appearing";
    public const string PersonMoves = "a person moving";
    public const string RelationshipJoin = "a relationship-gated join";
    public const string RecruitJoins = "a recruit joining";
    public const string Departure = "a departure";
    public const string LineupPicked = "picking the lineup";
    public const string HandOff = "the hand-off";

    public static IReadOnlyList<string> All { get; } =
    [
        Shift, LockedFallback, TwoArchetypes, WrittenOverride, Silence, TellStrengths, Demeanors, HiddenOutcome, Memory,
        ConditionalOpening, ChainedScene, WalkOverScene, PersonAppears, PersonMoves, RelationshipJoin, RecruitJoins, Departure, LineupPicked, HandOff,
    ];

    private readonly Dictionary<string, string> _hits = [];
    private readonly HashSet<TellStrength> _strengths = [];
    private readonly HashSet<Demeanor> _demeanors = [];
    private readonly HashSet<string> _lockedSeen = [];
    private readonly HashSet<string> _reacted = [];
    private readonly HashSet<string> _silentWithWritten = [];
    private readonly Dictionary<string, HashSet<string>> _outcomes = [];
    private readonly Dictionary<string, HashSet<string>> _startSteps = [];
    private readonly Dictionary<string, HashSet<string>> _startSignatures = [];
    private readonly HashSet<string> _initialParty = [];
    private TownView? _previousTown;

    public IReadOnlyDictionary<string, string> Hits => _hits;

    public void Hit(string mechanic, string evidence) => _hits.TryAdd(mechanic, evidence);

    public void ObserveGameStart(SessionSnapshot snapshot)
    {
        foreach (var member in snapshot.Party!.Members)
        {
            _initialParty.Add(member.Id);
        }

        ObserveTown(snapshot, reset: true);
    }

    public void ObserveLoad(SessionSnapshot snapshot) => ObserveTown(snapshot, reset: true);

    /// <summary>A conversation has just opened (from a person, a spot, or a walk-over).</summary>
    public void ObserveConversationStart(SessionSnapshot snapshot, bool byWalkOver)
    {
        var view = snapshot.Conversation!;
        if (byWalkOver)
        {
            Hit(WalkOverScene, view.Id);
        }

        if (!_startSteps.TryGetValue(view.Id, out var steps))
        {
            _startSteps[view.Id] = steps = [];
        }

        steps.Add(view.StepId);
        if (steps.Count > 1)
        {
            Hit(ConditionalOpening, $"{view.Id} opened at {string.Join(" and ", steps)}");
        }

        var key = view.Id + "/" + view.StepId;
        if (!_startSignatures.TryGetValue(key, out var signatures))
        {
            _startSignatures[key] = signatures = [];
        }

        signatures.Add(string.Join("|", view.Lines.Select(l => l.Text)) + "#" + string.Join("|", view.Choices.Select(c => c.Id)));
        if (signatures.Count > 1)
        {
            Hit(Memory, $"{key} showed different lines or choices on a later visit");
        }

        ObserveView(view);
    }

    public void ObserveBeforeChoice(SessionSnapshot snapshot)
    {
        if (snapshot.Conversation is not { } view)
        {
            return;
        }

        ObserveView(view);
        foreach (var choice in view.Choices.Where(c => !c.Available))
        {
            _lockedSeen.Add(view.Id + "/" + choice.Id);
        }
    }

    public void ObserveChoice(SessionSnapshot before, string? choiceId, SessionSnapshot after)
    {
        var report = after.LastChoice!;
        var view = before.Conversation;
        if (view is not null && choiceId is not null)
        {
            var key = view.Id + "/" + choiceId;
            if (!_outcomes.TryGetValue(key, out var outcomes))
            {
                _outcomes[key] = outcomes = [];
            }

            outcomes.Add(after.Conversation?.StepId ?? "ended");
            if (outcomes.Count > 1 && view.Choices.Any(c => c.Id == choiceId && c.Available))
            {
                Hit(HiddenOutcome, $"{key} led to {string.Join(" and ", outcomes)}");
            }

            if (view.Choices.Any(c => !c.Available) && view.Choices.Any(c => c.Id == choiceId && c.Available))
            {
                Hit(LockedFallback, $"{view.Id}: took {choiceId} while {string.Join(", ", view.Choices.Where(c => !c.Available).Select(c => c.Id))} was locked");
            }

            if (report.Joined.Length > 0 && _lockedSeen.Contains(key))
            {
                Hit(RelationshipJoin, $"{key} was locked earlier and then joined {string.Join(", ", report.Joined)}");
            }
        }

        if (report.TraitChanges.Length > 0)
        {
            Hit(Shift, $"{report.ConversationId}/{report.ChoiceId} changed {string.Join(", ", report.TraitChanges.Select(t => t.Trait))}");
        }

        foreach (var reaction in report.Reactions)
        {
            _strengths.Add(reaction.Strength);
            _reacted.Add(reaction.CharacterId);
            if (reaction.Kind == ReactionKind.Written)
            {
                Hit(WrittenOverride, $"{report.ConversationId}/{report.ChoiceId} for {reaction.CharacterId}");
            }
        }

        if (report.Reactions.Any(r => r.Kind == ReactionKind.Written) && view is not null)
        {
            foreach (var participant in view.Participants.Where(p => report.Reactions.All(r => r.CharacterId != p.Id)))
            {
                _silentWithWritten.Add(participant.Id);
            }
        }

        var archetype = report.Reactions.Where(r => r.Kind != ReactionKind.Written).ToList();
        if (archetype.Select(r => r.CharacterId).Distinct().Count() > 1 && archetype.Select(r => r.Direction).Distinct().Count() > 1)
        {
            Hit(TwoArchetypes, $"{report.ConversationId}/{report.ChoiceId}: " + string.Join(", ", archetype.Select(r => $"{r.CharacterId} {r.Direction}")));
        }

        if (report.NextConversationId is not null)
        {
            Hit(ChainedScene, $"{report.ConversationId}/{report.ChoiceId} led into {report.NextConversationId}");
        }

        foreach (var joined in report.Joined.Where(j => !_initialParty.Contains(j)))
        {
            Hit(RecruitJoins, joined);
        }

        if (report.Left.Length > 0)
        {
            Hit(Departure, string.Join(", ", report.Left));
        }

        ObserveTown(after, reset: false);
        ObserveHandOff(after);
    }

    public void ObserveAfterMove(SessionSnapshot snapshot)
    {
        ObserveTown(snapshot, reset: false);
        ObserveHandOff(snapshot);
    }

    public void ObserveLineup(SessionSnapshot snapshot)
    {
        if (snapshot.Party!.Lineup.Length > 0)
        {
            Hit(LineupPicked, string.Join(", ", snapshot.Party.Lineup.Select(m => m.Id)));
        }
    }

    public void ObserveHandOff(SessionSnapshot snapshot)
    {
        if (snapshot.HandOff is { } handOff)
        {
            Hit(HandOff, handOff.FightId);
        }
    }

    /// <summary>The mechanics with no evidence, in checklist order.</summary>
    public IReadOnlyList<string> Missing()
    {
        if (_strengths.Count >= 2)
        {
            Hit(TellStrengths, string.Join(", ", _strengths));
        }

        if (_demeanors.Count >= 2)
        {
            Hit(Demeanors, string.Join(", ", _demeanors));
        }

        if (_silentWithWritten.Overlaps(_reacted))
        {
            Hit(Silence, string.Join(", ", _silentWithWritten.Intersect(_reacted)));
        }

        return [.. All.Where(m => !_hits.ContainsKey(m))];
    }

    private void ObserveView(ConversationView view)
    {
        foreach (var participant in view.Participants)
        {
            _demeanors.Add(participant.Demeanor);
        }
    }

    private void ObserveTown(SessionSnapshot snapshot, bool reset)
    {
        var town = snapshot.Town;
        if (town is not null)
        {
            foreach (var person in town.People)
            {
                _demeanors.Add(person.Demeanor);
            }

            if (!reset && _previousTown is not null)
            {
                foreach (var person in town.People)
                {
                    var was = _previousTown.People.FirstOrDefault(p => p.CharacterId == person.CharacterId);
                    if (was is null)
                    {
                        Hit(PersonAppears, person.CharacterId);
                    }
                    else if (was.Position != person.Position)
                    {
                        Hit(PersonMoves, $"{person.CharacterId} from {was.Position} to {person.Position}");
                    }
                }

                foreach (var spot in town.Spots.Where(s => _previousTown.Spots.All(p => p.Id != s.Id)))
                {
                    Hit(PersonAppears, spot.Id);
                }
            }
        }

        _previousTown = town;
    }
}
