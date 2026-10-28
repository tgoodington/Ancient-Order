using System.Collections.Immutable;
using AncientOrder.Rules.Combat.Ai;
using AncientOrder.Rules.Common;

namespace AncientOrder.Rules.Combat;

public static class RoundManager
{
    /// <summary>Enemies never choose GROUP until the combat balance review designs enemy group attacks.</summary>
    private static readonly EvaluatorConfig EnemyConfig = new(false);

    /// <summary>Plays one round with the fight's own dice and returns the new state with Dice advanced.</summary>
    public static CombatState RunRound(CombatState state, IReadOnlyList<CombatAction> playerOrders, ArchetypeCatalog archetypes)
    {
        var rolls = SeededRolls.FromState(state.Dice);
        var next = RunRound(state, playerOrders, archetypes, rolls);
        var dice = rolls.State;
        var last = next.RoundHistory[^1];
        var record = last with { StateSnapshot = last.StateSnapshot with { Dice = dice } };
        return next with { Dice = dice, RoundHistory = next.RoundHistory.SetItem(next.RoundHistory.Length - 1, record) };
    }

    /// <summary>Plays one round with the given rolls (the test seam); Dice is left unchanged.</summary>
    public static CombatState RunRound(CombatState state, IReadOnlyList<CombatAction> playerOrders, ArchetypeCatalog archetypes, IRollSource rolls)
    {
        // Phase 1: enemy decisions, living enemies in enemy-party order.
        var enemyActions = new List<CombatAction>();
        foreach (var enemy in state.EnemyParty)
        {
            if (!enemy.IsKO)
            {
                enemyActions.Add(EnemyDecisions.Decide(enemy, state, archetypes, EnemyConfig));
            }
        }

        // Phase 3: player orders (invalid ones are dropped, as today).
        var batch = Declarations.ValidateAll(state, playerOrders);

        // Phase 4: queue, enemies then players, in resolution order.
        List<CombatAction> all = [.. enemyActions, .. batch.Accepted];
        var queue = Pipeline.Order(all, state, rolls);
        var stateWithQueue = state with { Phase = CombatPhase.PerAttack, ActionQueue = queue };

        // Phase 5: resolve one by one.
        var current = stateWithQueue;
        var results = ImmutableArray.CreateBuilder<ActionResult>();
        foreach (var action in queue)
        {
            var resolution = Pipeline.ResolveAction(current, action, rolls);
            current = resolution.State;
            results.Add(resolution.Result);
        }

        var status = CheckVictoryDefeat(current);

        var round = new RoundResult(
            state.Round,
            results.ToImmutable(),
            current with { Status = status, RoundHistory = [] });

        return current with
        {
            Round = state.Round + 1,
            Phase = CombatPhase.AiDecision,
            ActionQueue = [],
            RoundHistory = current.RoundHistory.Add(round),
            Status = status,
        };
    }

    /// <summary>All players KO is defeat (checked first, so a double wipe is a defeat); all enemies KO is victory.</summary>
    private static CombatStatus CheckVictoryDefeat(CombatState state)
    {
        if (state.PlayerParty.All(c => c.IsKO))
        {
            return CombatStatus.Defeat;
        }

        return state.EnemyParty.All(c => c.IsKO) ? CombatStatus.Victory : CombatStatus.Active;
    }
}
