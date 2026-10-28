using System.Collections.Immutable;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Party;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Combat;

/// <summary>Sets up a fight from the player, the lineup, and an encounter; writes a fight's lasting results back to the game state.</summary>
public static class FightSetup
{
    public static Result<GameState> Start(GameContent content, GameState state, string encounterId, ulong diceSeed, long now)
    {
        if (!content.Encounters.TryGet(encounterId, out var encounter))
        {
            return Fail(ErrorCode.ENCOUNTER_NOT_FOUND, $"Encounter '{encounterId}' not found in registry");
        }

        if (state.Combat is not null)
        {
            return Fail(ErrorCode.COMBAT_ALREADY_ACTIVE, "A combat encounter is already active");
        }

        const string lineupMessage = "Choose a full lineup before starting a fight";
        if (state.Lineup.IsDefault || state.Lineup.Length != content.Settings.LineupSize)
        {
            return Fail(ErrorCode.TEAM_COMPOSITION_INVALID, lineupMessage);
        }

        if (state.Lineup.Length == 0 && content.Settings.PlayerCombat is null)
        {
            return Fail(ErrorCode.TEAM_COMPOSITION_INVALID, lineupMessage);
        }

        var party = ImmutableArray.CreateBuilder<Combatant>();
        if (content.Settings.PlayerCombat is { } playerProfile)
        {
            party.Add(FromProfile(state.Player.Id, state.Player.Name, null, playerProfile));
        }

        foreach (var id in state.Lineup)
        {
            if (!content.Characters.TryGet(id, out var definition) || definition.Combat is null)
            {
                return Fail(ErrorCode.TEAM_COMPOSITION_INVALID, lineupMessage);
            }

            party.Add(FromProfile(definition.Id, definition.Name, definition.ArchetypeId, definition.Combat));
        }

        // Build, then seed from reaction progress, then apply synergy (same order as today).
        var modifiers = StatModifiers.FromSynergy(Synergy.Current(content, state));
        var playerParty = party
            .Select(c => ApplySynergy(Seed(c, state), modifiers))
            .ToImmutableArray();

        var enemyParty = encounter.Enemies.Select(FromConfig).ToImmutableArray();

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fighter in playerParty.Concat(enemyParty))
        {
            if (!ids.Add(fighter.Id))
            {
                return Fail(ErrorCode.TEAM_COMPOSITION_INVALID, $"Duplicate fighter id: {fighter.Id}");
            }
        }

        foreach (var fighter in playerParty)
        {
            var invalid = InvalidStat(fighter);
            if (invalid is not null)
            {
                return Fail(ErrorCode.VALIDATION_ERROR, $"Fight setup gave {fighter.Id} an invalid {invalid}");
            }
        }

        var combat = new CombatState(
            1,
            CombatPhase.AiDecision,
            playerParty,
            enemyParty,
            ImmutableArray<CombatAction>.Empty,
            ImmutableArray<RoundResult>.Empty,
            CombatStatus.Active,
            DiceState.FromSeed(diceSeed));

        return Result<GameState>.Ok(Sync(state, combat, now));
    }

    /// <summary>Stores the fight and copies each player-side fighter's reaction progress into the game state by id. Enemies never.</summary>
    public static GameState Sync(GameState state, CombatState combat, long now)
    {
        var progress = state.ReactionProgress;
        foreach (var combatant in combat.PlayerParty)
        {
            if (combatant.ReactionProgress is not null)
            {
                progress = progress.SetItem(combatant.Id, combatant.ReactionProgress);
            }
        }

        return GameStateUpdates.WithCombat(state, combat, now) with { ReactionProgress = progress };
    }

    public static GameState End(GameState state, long now) => GameStateUpdates.WithCombat(state, null, now);

    private static string? InvalidStat(Combatant c)
    {
        if (!double.IsFinite(c.Rank) || c.Rank <= 0)
        {
            return "rank";
        }

        if (!double.IsFinite(c.MaxStamina) || c.MaxStamina <= 0)
        {
            return "max stamina";
        }

        if (!double.IsFinite(c.Stamina) || c.Stamina <= 0)
        {
            return "stamina";
        }

        if (!double.IsFinite(c.Power) || c.Power < 0)
        {
            return "power";
        }

        return !double.IsFinite(c.Speed) || c.Speed < 0 ? "speed" : null;
    }

    private static Result<GameState> Fail(ErrorCode code, string message) =>
        Result<GameState>.Fail(new RuleError(code, message));

    private static Combatant Build(string id, string name, string? archetype, double rank, double stamina, double power, double speed, ElementalPath path, ReactionSkills skills, AscensionLevel level)
    {
        var startingEnergy = CombatConstants.AscensionStartingSegments[(int)level];
        return new Combatant(
            id,
            name,
            archetype,
            rank,
            stamina,
            stamina,
            power,
            speed,
            startingEnergy,
            startingEnergy > 0 ? startingEnergy : 3,
            level,
            ImmutableArray<Buff>.Empty,
            path,
            skills,
            null,
            false);
    }

    // Player-side reaction skills are replaced when seeded, so the placeholder here is never seen.
    private static Combatant FromProfile(string id, string name, string? archetype, CombatProfile p) =>
        Build(id, name, archetype, p.Rank, p.Stamina, p.Power, p.Speed, p.ElementalPath, ReactionProgression.ReactionSkillsFromProgress(ReactionProgression.Zero), p.AscensionLevel);

    private static Combatant FromConfig(CombatantConfig c) =>
        Build(c.Id, c.Name, c.Archetype, c.Rank, c.Stamina, c.Power, c.Speed, c.ElementalPath, c.ReactionSkills, c.AscensionLevel ?? AscensionLevel.Level0);

    private static Combatant Seed(Combatant combatant, GameState state)
    {
        var progress = state.ReactionProgress.TryGetValue(combatant.Id, out var found) ? found : ReactionProgression.Zero;
        return combatant with
        {
            ReactionProgress = progress,
            ReactionSkills = ReactionProgression.ReactionSkillsFromProgress(progress),
        };
    }

    // A fresh fight starts at full stamina, so a MaxStamina modifier sets both.
    private static Combatant ApplySynergy(Combatant c, ImmutableArray<StatModifier> modifiers)
    {
        if (modifiers.IsDefaultOrEmpty)
        {
            return c;
        }

        var maxStamina = StatModifiers.Apply(CombatStat.MaxStamina, c.MaxStamina, modifiers);
        return c with
        {
            Rank = StatModifiers.Apply(CombatStat.Rank, c.Rank, modifiers),
            MaxStamina = maxStamina,
            Stamina = StatModifiers.ForStat(CombatStat.MaxStamina, modifiers).IsEmpty ? c.Stamina : maxStamina,
            Power = StatModifiers.Apply(CombatStat.Power, c.Power, modifiers),
            Speed = StatModifiers.Apply(CombatStat.Speed, c.Speed, modifiers),
        };
    }
}
