using System.Collections.Immutable;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Combat.Ai;
using AncientOrder.Rules.Conditions;
using AncientOrder.Rules.Content.Packs.Format;
using AncientOrder.Rules.Conversations;
using AncientOrder.Rules.Effects;
using AncientOrder.Rules.Party;
using AncientOrder.Rules.Personality;
using AncientOrder.Rules.Reactions;
using AncientOrder.Rules.State;
using AncientOrder.Rules.Town;

namespace AncientOrder.Rules.Content.Packs;

/// <summary>Maps the format records to the rules records. Checking is left to GameContent.Create; a value the format already guarantees is not rechecked.</summary>
public static class ContentMapper
{
    public static GameSettings ToRules(SettingsFormat s) =>
        new(s.PlayerName, new LineupLimits(s.Lineup.Min, s.Lineup.Max), s.StartingPersonality is null ? null : ToRules(s.StartingPersonality), s.PlayerCombat is null ? null : ToRules(s.PlayerCombat))
        {
            StartingParty = s.StartingParty.IsDefault ? [] : s.StartingParty,
            Conversations = new ConversationSettings(s.Conversations?.LockedText ?? ConversationSettings.DefaultLockedText),
            Tells = s.Tells is null ? new TellSettings() : new TellSettings(s.Tells.ClearAt, s.Tells.StrongAt),
            Demeanor = s.Demeanor is null ? new DemeanorSettings() : new DemeanorSettings(s.Demeanor.WarmAt, s.Demeanor.WaryAt, s.Demeanor.ColdAt),
            StartingTown = s.StartingTown,
            Reach = s.Reach ?? GameSettings.DefaultReach,
            Display = s.Display is null ? new DisplaySettings() : new DisplaySettings(s.Display.PartyReadouts, s.Display.SynergyNumbers),
        };

    public static AncientOrder.Rules.Personality.Personality ToRules(PersonalityFormat p) =>
        new()
        {
            Patience = p.Patience,
            Empathy = p.Empathy,
            Cunning = p.Cunning,
            Logic = p.Logic,
            Kindness = p.Kindness,
            Charisma = p.Charisma,
        };

    public static CombatProfile ToRules(CombatProfileFormat c) =>
        new(c.Rank, c.Stamina, c.Power, c.Speed, c.ElementalPath, c.AscensionLevel ?? AscensionLevel.Level0);

    public static CharacterDefinition ToRules(CharacterFormat c) =>
        new(c.Id, c.Name, c.ArchetypeId, ToRules(c.Personality), c.StartingAffection, c.StartingTrust, c.Combat is null ? null : ToRules(c.Combat));

    public static ArchetypeDefinition ToRules(ArchetypeFormat a) =>
        new(a.Id, a.Profile is null ? new ArchetypeBehavior.Basic() : new ArchetypeBehavior.Scored(ToRules(a.Profile)), a.Reactions is null ? null : ToRules(a.Reactions));

    private static ReactionProfile ToRules(ReactionsFormat r) =>
        new(
            new ReactionLeanings(r.Leanings.Patience ?? 0, r.Leanings.Empathy ?? 0, r.Leanings.Cunning ?? 0, r.Leanings.Logic ?? 0, r.Leanings.Kindness ?? 0, r.Leanings.Charisma ?? 0),
            r.Minimum,
            r.AffectionPerPoint,
            r.TrustPerPoint,
            new ReactionLines(r.Approve.Clear, r.Approve.Slight, r.Approve.Strong),
            new ReactionLines(r.Disapprove.Clear, r.Disapprove.Slight, r.Disapprove.Strong));

    public static SynergySettings ToRules(SynergyFormat s) =>
        new([.. s.Bonuses.Select(b => new SynergyBonusDefinition(b.Name, b.Rule, b.Threshold, b.Stat, b.Step, b.Value, b.Feel))], s.Resolution);

    public static EncounterDefinition ToRules(FightFormat f) =>
        new(f.Id, f.Name, [.. f.Enemies.Select(ToRules)]) { Lineup = f.Lineup is null ? null : new LineupLimits(f.Lineup.Min, f.Lineup.Max) };

    public static ConversationDefinition ToRules(ConversationFormat c) =>
        new(
            c.Id,
            c.Title,
            c.Participants.IsDefault ? [] : c.Participants,
            c.Start.IsDefault ? [] : [.. c.Start.Select(s => new StartEntry(s.Step) { When = ToRules(s.When) })],
            [.. c.Steps.Select(ToRules)]);

    public static TownDefinition ToRules(TownFormat t) =>
        new(
            t.Id,
            t.Name,
            [.. t.Ground.Select(ToPolygon)],
            t.Obstacles.IsDefault ? [] : [.. t.Obstacles.Select(o => new ObstacleDefinition(o.Id, ToPolygon(o.Footprint), o.Height, o.Label))],
            ToPoint(t.Start),
            t.People.IsDefault ? [] : [.. t.People.Select(p => new TownPerson(p.Character, [.. p.Placements.Select(l => new PersonPlacement(ToPoint(l.At), l.Facing) { When = ToRules(l.When) })]) { Opens = ToRules(p.Opens) })],
            t.Spots.IsDefault ? [] : [.. t.Spots.Select(s => new SpotDefinition(s.Id, ToArea(s.Area), s.Trigger, s.Name, s.Shown) { When = ToRules(s.When), Opens = ToRules(s.Opens) })],
            t.Reach);

    private static ImmutableArray<OpensEntry> ToRules(ImmutableArray<OpensFormat> opens) =>
        opens.IsDefault ? [] : [.. opens.Select(o => new OpensEntry(o.Conversation, o.HandOff) { When = ToRules(o.When) })];

    private static Area ToArea(AreaFormat a) =>
        a.Circle is { } c ? new Area.Circle(ToPoint(c.Center), c.Radius) : new Area.PolygonArea(ToPolygon(a.Polygon));

    private static Polygon ToPolygon(ImmutableArray<ImmutableArray<double>> points) =>
        new([.. points.Select(ToPoint)]);

    // The pack check has already refused any point that is not exactly two numbers.
    private static Point ToPoint(ImmutableArray<double> pair) => new(pair[0], pair[1]);

    private static StepDefinition ToRules(StepFormat s) =>
        new(
            s.Id,
            s.Lines.IsDefault ? [] : [.. s.Lines.Select(l => new LineDefinition(Speaker.FromId(l.Speaker), l.Text) { When = ToRules(l.When) })],
            s.Choices.IsDefault ? [] : [.. s.Choices.Select(ToRules)],
            ToRules(s.Next));

    private static ChoiceDefinition ToRules(ChoiceFormat c) =>
        new(
            c.Id,
            c.Text,
            c.LockedText,
            c.Effects.IsDefault ? [] : [.. c.Effects.Select(ToRules)],
            ToRules(c.Next))
        {
            When = ToRules(c.When),
            Requires = ToRules(c.Requires),
            Reactions = c.Reactions.IsDefault ? [] : [.. c.Reactions.Select(r => new ReactionOverride(r.Character, r.Silence ?? false, r.Text, r.Affection, r.Trust))],
        };

    private static Effect ToRules(EffectFormat e) => e switch
    {
        RelationshipEffectFormat r => new Effect.Relationship(r.Character, r.Affection, r.Trust),
        JoinEffectFormat j => new Effect.Join(j.Character),
        LeaveEffectFormat l => new Effect.Leave(l.Character),
        ShiftEffectFormat h => new Effect.Shift(new PersonalityAdjustment(ShiftDeltas(h))),
        SetFlagEffectFormat s => new Effect.SetFlag(s.Flag),
        ClearFlagEffectFormat c => new Effect.ClearFlag(c.Flag),
        HandOffEffectFormat o => new Effect.HandOff(o.Fight),
        _ => throw new InvalidOperationException($"Unknown effect format {e.GetType().Name}"),
    };

    private static ImmutableDictionary<Trait, double> ShiftDeltas(ShiftEffectFormat h)
    {
        var deltas = ImmutableDictionary.CreateBuilder<Trait, double>();
        void Add(Trait trait, double? value)
        {
            if (value is { } v)
            {
                deltas[trait] = v;
            }
        }

        Add(Trait.Patience, h.Patience);
        Add(Trait.Empathy, h.Empathy);
        Add(Trait.Cunning, h.Cunning);
        Add(Trait.Logic, h.Logic);
        Add(Trait.Kindness, h.Kindness);
        Add(Trait.Charisma, h.Charisma);
        return deltas.ToImmutable();
    }

    // A malformed destination (neither or both set) is already reported by the pack check and is never played.
    private static ImmutableArray<Destination> ToRules(ImmutableArray<DestinationFormat> next) =>
        next.IsDefault
            ? []
            : [.. next.Select(d => d.Conversation is not null && d.Step is null
                ? (Destination)new Destination.ToConversation(d.Conversation) { When = ToRules(d.When) }
                : new Destination.ToStep(d.Step ?? string.Empty) { When = ToRules(d.When) })];

    private static ImmutableArray<Condition> ToRules(ImmutableArray<ConditionFormat> conditions) =>
        conditions.IsDefault ? [] : [.. conditions.Select(ToRules)];

    private static Condition ToRules(ConditionFormat c) => c switch
    {
        FlagConditionFormat f => new Condition.Flag(f.Flag),
        ChoseConditionFormat o => new Condition.Chose(o.Conversation, o.Choice),
        SeenConditionFormat s => new Condition.Seen(s.Conversation),
        TraitConditionFormat t => new Condition.TraitRange(t.Trait, t.AtLeast, t.AtMost),
        RelationshipConditionFormat r => new Condition.RelationshipRange(r.Character, r.Measure, r.AtLeast, r.AtMost),
        InPartyConditionFormat p => new Condition.InParty(p.Character),
        LineupReadyConditionFormat l => new Condition.LineupReady(l.Fight),
        AllConditionFormat a => new Condition.All(ToRules(a.Conditions)),
        AnyConditionFormat a => new Condition.Any(ToRules(a.Conditions)),
        NotConditionFormat n => new Condition.Negation(ToRules(n.Condition)),
        _ => throw new InvalidOperationException($"Unknown condition format {c.GetType().Name}"),
    };

    private static DecisionProfile ToRules(DecisionProfileFormat p) =>
        new(
            new Dictionary<ActionType, double>
            {
                [ActionType.Attack] = p.BaseScores.Attack,
                [ActionType.Defend] = p.BaseScores.Defend,
                [ActionType.Evade] = p.BaseScores.Evade,
                [ActionType.Special] = p.BaseScores.Special,
                [ActionType.Group] = p.BaseScores.Group,
            }.ToImmutableDictionary(),
            p.FactorWeights.ToImmutableDictionary(StringComparer.Ordinal));

    private static CombatantConfig ToRules(EnemyFormat e) =>
        new(
            e.Id,
            e.Name,
            e.Archetype,
            e.Rank,
            e.Stamina,
            e.Power,
            e.Speed,
            e.ElementalPath,
            new ReactionSkills(ToRules(e.ReactionSkills.Block), ToRules(e.ReactionSkills.Dodge), ToRules(e.ReactionSkills.Parry)),
            e.AscensionLevel);

    private static DefenseSkill ToRules(DefenseSkillFormat d) => new(d.Sr, d.Smr, d.Fmr);
}
