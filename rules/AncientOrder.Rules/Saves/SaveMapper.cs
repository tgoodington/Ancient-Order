using System.Collections.Immutable;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Saves.Format;
using AncientOrder.Rules.State;
using AncientOrder.Rules.Story;
using PersonalityRecord = AncientOrder.Rules.Personality.Personality;

namespace AncientOrder.Rules.Saves;

/// <summary>GameState to and from the format records. The only place that knows both shapes.</summary>
public static class SaveMapper
{
    /// <summary>
    /// Characters go out in catalog order; any character not in the catalog follows, sorted by id (ordinal), so none is lost.
    /// Reaction progress is sorted by id (ordinal).
    /// </summary>
    public static SavedGame ToSaved(GameState game, GameContent content)
    {
        var characters = ImmutableArray.CreateBuilder<SavedCharacter>();
        foreach (var definition in content.Characters.Definitions)
        {
            if (game.Characters.TryGetValue(definition.Id, out var state))
            {
                characters.Add(new SavedCharacter(definition.Id, state.Affection, state.Trust));
            }
        }

        var extras = game.Characters
            .Where(pair => !content.Characters.TryGet(pair.Key, out _))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal);
        foreach (var pair in extras)
        {
            characters.Add(new SavedCharacter(pair.Key, pair.Value.Affection, pair.Value.Trust));
        }

        var progress = game.ReactionProgress
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new SavedReactionProgress(pair.Key, pair.Value.Block, pair.Value.Dodge, pair.Value.Parry))
            .ToImmutableArray();

        return new SavedGame(
            game.Timestamp,
            new SavedPlayer(game.Player.Id, game.Player.Name, ToSaved(game.Player.Personality)),
            characters.ToImmutable(),
            game.Lineup,
            progress,
            game.Combat is null ? null : ToSaved(game.Combat),
            game.Party.IsDefault ? [] : game.Party,
            game.Town is null ? null : new SavedTown(game.Town.TownId, game.Town.Position.X, game.Town.Position.Z),
            game.Conversation is null ? null : new SavedConversation(game.Conversation.ConversationId, game.Conversation.StepId),
            ToSaved(game.Memory),
            game.HandOff is null ? null : new SavedHandOff(game.HandOff.FightId, game.HandOff.Lineup.IsDefault ? [] : game.HandOff.Lineup, game.HandOff.TownId));
    }

    /// <summary>
    /// The summary the slot list shows: what the player was doing (hand-off, else conversation, else town, else none) and the name of
    /// the town they were in, from the content at save time.
    /// </summary>
    public static SaveSummary ToSummary(GameState game, GameContent content)
    {
        var situation = game.HandOff is not null
            ? SaveSituation.HandOff
            : game.Conversation is not null
                ? SaveSituation.Conversation
                : game.Town is not null ? SaveSituation.Town : SaveSituation.None;
        var townId = game.Town?.TownId ?? game.HandOff?.TownId;
        string? name = null;
        if (townId is not null && content.Towns.TryGet(townId, out var town) && !string.IsNullOrEmpty(town.Name))
        {
            name = new string([.. town.Name.Where(c => !char.IsControl(c))]);
            if (name.Length > SaveConstants.MaxTownNameLength)
            {
                var cut = SaveConstants.MaxTownNameLength;
                name = name[..(char.IsHighSurrogate(name[cut - 1]) ? cut - 1 : cut)];
            }
        }

        return new SaveSummary(situation, name);
    }

    /// <summary>The one place the memory sets are iterated: flags and seen ordinal, choices by conversation then choice (ordinal).</summary>
    private static SavedMemory ToSaved(StoryMemory memory) => new(
        [.. memory.Flags.Order(StringComparer.Ordinal)],
        [.. memory.Chosen
            .OrderBy(key => key.ConversationId, StringComparer.Ordinal)
            .ThenBy(key => key.ChoiceId, StringComparer.Ordinal)
            .Select(key => new SavedChoice(key.ConversationId, key.ChoiceId))],
        [.. memory.Seen.Order(StringComparer.Ordinal)]);

    public static StoryMemory ToMemory(SavedMemory memory) => new(
        ImmutableHashSet.CreateRange(StringComparer.Ordinal, memory.Flags),
        ImmutableHashSet.CreateRange(ChoiceKeyComparer.Instance, memory.Chosen.Select(c => new ChoiceKey(c.Conversation, c.Choice))),
        ImmutableHashSet.CreateRange(StringComparer.Ordinal, memory.Seen));

    public static PlayerCharacter ToPlayer(SavedPlayer player) =>
        new(player.Id, player.Name, ToPersonality(player.Personality));

    public static PersonalityRecord ToPersonality(SavedPersonality p) => new()
    {
        Patience = p.Patience,
        Empathy = p.Empathy,
        Cunning = p.Cunning,
        Logic = p.Logic,
        Kindness = p.Kindness,
        Charisma = p.Charisma,
    };

    /// <summary>XP values must already have passed SaveChecks (whole, 0 up to int.MaxValue).</summary>
    public static ReactionProgress ToProgress(double block, double dodge, double parry) =>
        new((int)block, (int)dodge, (int)parry);

    private static SavedPersonality ToSaved(PersonalityRecord p) =>
        new(p.Patience, p.Empathy, p.Cunning, p.Logic, p.Kindness, p.Charisma);

    // ---- Combat mirror: every combat record is mapped below, in both directions. ----

    public static SavedCombat ToSaved(CombatState c) => new(
        c.Round,
        c.Phase,
        [.. c.PlayerParty.Select(ToSaved)],
        [.. c.EnemyParty.Select(ToSaved)],
        [.. c.ActionQueue.Select(ToSaved)],
        [.. c.RoundHistory.Select(ToSaved)],
        c.Status,
        new SavedDice(c.Dice.S0, c.Dice.S1, c.Dice.S2, c.Dice.S3));

    public static CombatState ToCombat(SavedCombat c) => new(
        c.Round,
        c.Phase,
        [.. c.PlayerParty.Select(ToCombatant)],
        [.. c.EnemyParty.Select(ToCombatant)],
        [.. c.ActionQueue.Select(ToAction)],
        [.. c.RoundHistory.Select(ToRound)],
        c.Status,
        new DiceState(c.Dice.S0, c.Dice.S1, c.Dice.S2, c.Dice.S3));

    private static SavedCombatant ToSaved(Combatant f) => new(
        f.Id,
        f.Name,
        f.ArchetypeId,
        f.Rank,
        f.Stamina,
        f.MaxStamina,
        f.Power,
        f.Speed,
        f.Energy,
        f.MaxEnergy,
        f.AscensionLevel,
        [.. f.ActiveBuffs.Select(b => new SavedBuff(b.Stat, b.Source, b.Modifier))],
        f.ElementalPath,
        new SavedReactionSkills(ToSaved(f.ReactionSkills.Block), ToSaved(f.ReactionSkills.Dodge), ToSaved(f.ReactionSkills.Parry)),
        f.ReactionProgress is null ? null : new SavedCombatReactionProgress(f.ReactionProgress.Block, f.ReactionProgress.Dodge, f.ReactionProgress.Parry),
        f.IsKO);

    private static Combatant ToCombatant(SavedCombatant f) => new(
        f.Id,
        f.Name,
        f.ArchetypeId,
        f.Rank,
        f.Stamina,
        f.MaxStamina,
        f.Power,
        f.Speed,
        f.Energy,
        f.MaxEnergy,
        f.AscensionLevel,
        [.. f.ActiveBuffs.Select(b => new Buff(b.Stat, b.Source, b.Modifier))],
        f.ElementalPath,
        new ReactionSkills(ToSkill(f.ReactionSkills.Block), ToSkill(f.ReactionSkills.Dodge), ToSkill(f.ReactionSkills.Parry)),
        f.ReactionProgress is null ? null : ToProgress(f.ReactionProgress.Block, f.ReactionProgress.Dodge, f.ReactionProgress.Parry),
        f.IsKO);

    private static SavedDefenseSkill ToSaved(DefenseSkill s) => new(s.SR, s.SMR, s.FMR);

    private static DefenseSkill ToSkill(SavedDefenseSkill s) => new(s.SR, s.SMR, s.FMR);

    private static SavedCombatAction ToSaved(CombatAction a) =>
        new(a.CombatantId, a.Type, a.TargetId, a.EnergySegments);

    private static CombatAction ToAction(SavedCombatAction a) =>
        new(a.CombatantId, a.Type, a.TargetId, a.EnergySegments);

    private static SavedRoundResult ToSaved(RoundResult r) =>
        new(r.Round, [.. r.Actions.Select(ToSaved)], ToSaved(r.StateSnapshot));

    private static RoundResult ToRound(SavedRoundResult r) =>
        new(r.Round, [.. r.Actions.Select(ToActionResult)], ToCombat(r.StateSnapshot));

    private static SavedActionResult ToSaved(ActionResult a) =>
        new(
            a.CombatantId,
            a.Type,
            a.AttackResult is null ? null : ToSaved(a.AttackResult),
            [.. a.CounterExchanges.Select(x => new SavedCounterExchange(
                x.AttackerId, x.DefenderId, x.Defense, new SavedDefenseResult(x.Outcome.Type, x.Outcome.Success, x.Outcome.DamageMultiplier), x.Damage))],
            a.EvadeRecovery);

    private static ActionResult ToActionResult(SavedActionResult a) =>
        new(a.CombatantId, a.Type, a.AttackResult is null ? null : ToAttack(a.AttackResult))
        {
            CounterExchanges = [.. a.CounterExchanges.Select(x => new CounterExchange(
                x.AttackerId, x.DefenderId, x.Defense, new DefenseResult(x.Outcome.Type, x.Outcome.Success, x.Outcome.DamageMultiplier), x.Damage))],
            EvadeRecovery = a.EvadeRecovery,
        };

    private static SavedAttackResult ToSaved(AttackResult r) => new(
        r.AttackerId,
        r.TargetId,
        r.Damage,
        r.DefenseType,
        new SavedDefenseResult(r.DefenseOutcome.Type, r.DefenseOutcome.Success, r.DefenseOutcome.DamageMultiplier),
        r.RankKO,
        r.Blindside,
        r.CrushingBlow,
        r.CounterChain);

    private static AttackResult ToAttack(SavedAttackResult r) => new(
        r.AttackerId,
        r.TargetId,
        r.Damage,
        r.DefenseType,
        new DefenseResult(r.DefenseOutcome.Type, r.DefenseOutcome.Success, r.DefenseOutcome.DamageMultiplier),
        r.RankKO,
        r.Blindside,
        r.CrushingBlow,
        r.CounterChain);
}
