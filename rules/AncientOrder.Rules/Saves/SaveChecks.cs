using System.Collections.Immutable;
using System.Globalization;
using AncientOrder.Rules.Personality;
using AncientOrder.Rules.Saves.Format;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Saves;

/// <summary>
/// The value checks on a mapped save. Reasons follow the TypeScript wording where an equivalent exists.
/// Enum values and numbers that overflow are already refused by the reader; those that reach here are checked again.
/// </summary>
internal static class SaveChecks
{
    private const double RelationshipLimit = 100;
    private const int MaxIdLength = CharacterCatalog.MaxIdLength;

    public static void CheckEnvelope(SaveEnvelope envelope, List<string> reasons)
    {
        if (envelope.Kind != SaveConstants.KindManual)
        {
            reasons.Add("kind: unsupported");
        }

        if (!SaveConstants.IsValidSlot(envelope.Slot))
        {
            reasons.Add("slot: expected integer between 1 and 10");
        }

        if (envelope.PlayerName is null)
        {
            reasons.Add("playerName: expected string");
        }

        CheckGame(envelope.Game, reasons);
    }

    public static void CheckGame(SavedGame game, List<string> reasons)
    {
        if (game is null)
        {
            reasons.Add("game: expected object");
            return;
        }

        CheckPlayer(game.Player, reasons);
        CheckCharacters(game.Characters, reasons);

        if (game.Lineup.IsDefault)
        {
            reasons.Add("lineup: expected array");
        }
        else if (game.Lineup.Any(BadId))
        {
            reasons.Add("lineup: each id must be 1 to 64 characters");
        }

        CheckReactionProgress(game.ReactionProgress, reasons);

        if (game.Combat is not null)
        {
            CheckCombat(game.Combat, "combat", true, reasons);
        }
    }

    private static void CheckPlayer(SavedPlayer? player, List<string> reasons)
    {
        if (player is null)
        {
            reasons.Add("player: expected object");
            return;
        }

        if (player.Id is null)
        {
            reasons.Add("player.id: expected string");
        }

        if (player.Name is null)
        {
            reasons.Add("player.name: expected string");
        }

        if (player.Personality is null)
        {
            reasons.Add("player.personality: expected object");
            return;
        }

        var validation = PersonalityRules.Validate(SaveMapper.ToPersonality(player.Personality));
        foreach (var error in validation.Errors)
        {
            reasons.Add("player.personality: " + error);
        }
    }

    private static void CheckCharacters(ImmutableArray<SavedCharacter> characters, List<string> reasons)
    {
        if (characters.IsDefault)
        {
            reasons.Add("characters: expected array");
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var character in characters)
        {
            if (character is null || BadId(character.Id))
            {
                reasons.Add("characters: each entry needs an id of 1 to 64 characters");
                continue;
            }

            if (!seen.Add(character.Id))
            {
                reasons.Add("characters: duplicate id " + character.Id);
            }

            CheckRelationship(character.Id, "affection", character.Affection, reasons);
            CheckRelationship(character.Id, "trust", character.Trust, reasons);
        }
    }

    private static void CheckRelationship(string id, string field, double value, List<string> reasons)
    {
        if (!double.IsFinite(value) || value < -RelationshipLimit || value > RelationshipLimit)
        {
            reasons.Add($"characters.{id}.{field}: value {Num(value)} is outside -100 to 100");
        }
    }

    private static void CheckReactionProgress(ImmutableArray<SavedReactionProgress> entries, List<string> reasons)
    {
        if (entries.IsDefault)
        {
            reasons.Add("reactionProgress: expected array");
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (entry is null || BadId(entry.Id))
            {
                reasons.Add("reactionProgress: each entry needs an id of 1 to 64 characters");
                continue;
            }

            if (!seen.Add(entry.Id))
            {
                reasons.Add("reactionProgress: duplicate id " + entry.Id);
            }

            CheckXp($"reactionProgress.{entry.Id}", entry.Block, entry.Dodge, entry.Parry, reasons);
        }
    }

    private static void CheckXp(string path, double block, double dodge, double parry, List<string> reasons)
    {
        CheckXpValue(path + ".block", block, reasons);
        CheckXpValue(path + ".dodge", dodge, reasons);
        CheckXpValue(path + ".parry", parry, reasons);
    }

    private static void CheckXpValue(string path, double value, List<string> reasons)
    {
        if (!double.IsFinite(value))
        {
            reasons.Add($"{path}: expected a finite number");
        }
        else if (value < 0)
        {
            reasons.Add($"{path}: value {Num(value)} is below 0");
        }
        else if (value > int.MaxValue || value != Math.Floor(value))
        {
            reasons.Add($"{path}: value {Num(value)} is not a whole number up to {int.MaxValue.ToString(CultureInfo.InvariantCulture)}");
        }
    }

    private static void CheckCombat(SavedCombat combat, string path, bool topLevel, List<string> reasons)
    {
        if (combat.Round < 1)
        {
            reasons.Add($"{path}.round: expected 1 or more");
        }

        if (topLevel && combat.Status != Combat.CombatStatus.Active)
        {
            reasons.Add($"{path}.status: a saved fight must be active");
        }

        // Phase, status, and the other enums are refused by the reader unless defined.
        if (topLevel && (combat.Dice is null || (combat.Dice.S0 | combat.Dice.S1 | combat.Dice.S2 | combat.Dice.S3) == 0))
        {
            reasons.Add($"{path}.dice: expected a non-zero dice state");
        }

        CheckParty(combat.PlayerParty, path + ".playerParty", reasons);
        CheckParty(combat.EnemyParty, path + ".enemyParty", reasons);

        if (combat.ActionQueue.IsDefault)
        {
            reasons.Add($"{path}.actionQueue: expected array");
        }
        else
        {
            for (var i = 0; i < combat.ActionQueue.Length; i++)
            {
                var action = combat.ActionQueue[i];
                if (action is null)
                {
                    reasons.Add($"{path}.actionQueue[{Idx(i)}]: expected object");
                }
                else if (BadId(action.CombatantId) || (action.TargetId is not null && BadId(action.TargetId)))
                {
                    reasons.Add($"{path}.actionQueue[{Idx(i)}]: ids must be 1 to 64 characters");
                }
                else if (action.EnergySegments is { } segments && !double.IsFinite(segments))
                {
                    reasons.Add($"{path}.actionQueue[{Idx(i)}].energySegments: expected a finite number");
                }
            }
        }

        if (combat.RoundHistory.IsDefault)
        {
            reasons.Add($"{path}.roundHistory: expected array");
            return;
        }

        for (var i = 0; i < combat.RoundHistory.Length; i++)
        {
            var round = combat.RoundHistory[i];
            var roundPath = $"{path}.roundHistory[{Idx(i)}]";
            if (round is null || round.StateSnapshot is null || round.Actions.IsDefault)
            {
                reasons.Add($"{roundPath}: expected a round with actions and a snapshot");
                continue;
            }

            CheckActionResults(round.Actions, roundPath + ".actions", reasons);
            CheckCombat(round.StateSnapshot, roundPath + ".stateSnapshot", false, reasons);
        }
    }

    private static void CheckActionResults(ImmutableArray<SavedActionResult> actions, string path, List<string> reasons)
    {
        for (var i = 0; i < actions.Length; i++)
        {
            var action = actions[i];
            if (action is null || BadId(action.CombatantId) || action.CounterExchanges.IsDefault)
            {
                reasons.Add($"{path}[{Idx(i)}]: expected an action with a valid id and counter exchanges");
                continue;
            }

            if (action.EvadeRecovery is { } recovery && !double.IsFinite(recovery))
            {
                reasons.Add($"{path}[{Idx(i)}].evadeRecovery: expected a finite number");
            }

            foreach (var x in action.CounterExchanges)
            {
                if (x is null || BadId(x.AttackerId) || BadId(x.DefenderId) || x.Outcome is null
                    || !double.IsFinite(x.Damage) || !double.IsFinite(x.Outcome.DamageMultiplier))
                {
                    reasons.Add($"{path}[{Idx(i)}].counterExchanges: expected valid ids, a defense outcome and finite numbers");
                    break;
                }
            }

            var attack = action.AttackResult;
            if (attack is null)
            {
                continue;
            }

            if (BadId(attack.AttackerId) || BadId(attack.TargetId))
            {
                reasons.Add($"{path}[{Idx(i)}].attackResult: ids must be 1 to 64 characters");
            }

            if (!double.IsFinite(attack.Damage) || attack.DefenseOutcome is null || !double.IsFinite(attack.DefenseOutcome.DamageMultiplier))
            {
                reasons.Add($"{path}[{Idx(i)}].attackResult: expected finite numbers and a defense outcome");
            }
        }
    }

    private static void CheckParty(ImmutableArray<SavedCombatant> party, string path, List<string> reasons)
    {
        if (party.IsDefault)
        {
            reasons.Add($"{path}: expected array");
            return;
        }

        for (var i = 0; i < party.Length; i++)
        {
            var fighter = party[i];
            var fighterPath = $"{path}[{Idx(i)}]";
            if (fighter is null)
            {
                reasons.Add($"{fighterPath}: expected object");
                continue;
            }

            CheckFighter(fighter, fighterPath, reasons);
        }
    }

    private static void CheckFighter(SavedCombatant f, string path, List<string> reasons)
    {
        if (BadId(f.Id) || f.Name is null || (f.ArchetypeId is not null && BadId(f.ArchetypeId)))
        {
            reasons.Add($"{path}: id must be 1 to 64 characters, and name is required");
        }

        var finite = double.IsFinite(f.Rank) && double.IsFinite(f.Stamina) && double.IsFinite(f.MaxStamina)
            && double.IsFinite(f.Power) && double.IsFinite(f.Speed) && double.IsFinite(f.Energy) && double.IsFinite(f.MaxEnergy);
        if (!finite)
        {
            reasons.Add($"{path}: stats must be finite numbers");
        }
        else if (f.Stamina < 0 || f.Stamina > f.MaxStamina)
        {
            reasons.Add($"{path}.stamina: value {Num(f.Stamina)} is outside 0 to maxStamina ({Num(f.MaxStamina)})");
        }

        if (f.ActiveBuffs.IsDefault || f.ActiveBuffs.Any(b => b is null || b.Source is null || !double.IsFinite(b.Modifier)))
        {
            reasons.Add($"{path}.activeBuffs: expected buffs with a source and a finite modifier");
        }

        var skills = f.ReactionSkills;
        if (skills is null || skills.Block is null || skills.Dodge is null || skills.Parry is null
            || !SkillFinite(skills.Block) || !SkillFinite(skills.Dodge) || !SkillFinite(skills.Parry))
        {
            reasons.Add($"{path}.reactionSkills: expected finite rates");
        }

        if (f.ReactionProgress is { } progress)
        {
            CheckXp(path + ".reactionProgress", progress.Block, progress.Dodge, progress.Parry, reasons);
        }
    }

    private static bool SkillFinite(SavedDefenseSkill s) =>
        double.IsFinite(s.SR) && double.IsFinite(s.SMR) && double.IsFinite(s.FMR);

    private static bool BadId(string? id) => string.IsNullOrEmpty(id) || id.Length > MaxIdLength;

    private static string Num(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string Idx(int i) => i.ToString(CultureInfo.InvariantCulture);
}
