namespace AncientOrder.Rules.Combat.Ai;

/// <summary>GroupActionsEnabled is a switch; T7 sets it false in play (enemies do not choose GROUP until the combat balance review).</summary>
public sealed record EvaluatorConfig(bool GroupActionsEnabled)
{
    public static EvaluatorConfig Default { get; } = new(false);
}

public static class EnemyDecisions
{
    /// <summary>
    /// Decides one combatant's action. An archetype missing from the catalog is a bug (content validation prevents it) and throws.
    /// </summary>
    public static CombatAction Decide(Combatant combatant, CombatState state, ArchetypeCatalog archetypes, EvaluatorConfig config)
    {
        if (!archetypes.TryGet(combatant.ArchetypeId, out var archetype))
        {
            throw new InvalidOperationException($"No archetype found for archetype: \"{combatant.ArchetypeId}\".");
        }

        return archetype.Behavior switch
        {
            ArchetypeBehavior.Basic => DecideBasic(combatant, state),
            ArchetypeBehavior.Scored scored => DecideScored(combatant, state, scored.Profile, config),
            _ => throw new InvalidOperationException("Unknown archetype behavior."),
        };
    }

    private static CombatAction DecideBasic(Combatant combatant, CombatState state)
    {
        var isInPlayerParty = state.PlayerParty.Any(c => c.Id == combatant.Id);
        var opponents = isInPlayerParty ? state.EnemyParty : state.PlayerParty;
        var target = opponents.FirstOrDefault(c => !c.IsKO);
        return new CombatAction(combatant.Id, ActionType.Attack, target?.Id, null);
    }

    private static CombatAction DecideScored(Combatant combatant, CombatState state, DecisionProfile profile, EvaluatorConfig config)
    {
        var perception = Perception.Build(combatant, state);
        var coeff = RankCoefficient.For(combatant.Rank);

        // Candidate generation order matters for ties: ATTACK, DEFEND, EVADE, SPECIAL, GROUP (as today).
        var actionTypes = new List<ActionType> { ActionType.Attack, ActionType.Defend, ActionType.Evade };
        if (combatant.Energy > 0)
        {
            actionTypes.Add(ActionType.Special);
        }

        if (config.GroupActionsEnabled)
        {
            actionTypes.Add(ActionType.Group);
        }

        var candidates = new List<ScoredCandidate>();
        foreach (var actionType in actionTypes)
        {
            var targets = new List<(string? Id, TargetPerception? Target)>();
            switch (actionType)
            {
                case ActionType.Attack:
                case ActionType.Special:
                    foreach (var e in perception.Enemies.Where(e => !e.IsKO))
                    {
                        targets.Add((e.Id, new TargetPerception(e.Id, e.StaminaPct, e.SpeedDelta, e.RankDelta, e.Power)));
                    }

                    break;
                case ActionType.Defend:
                    foreach (var a in perception.Allies.Where(a => !a.IsKO))
                    {
                        targets.Add((a.Id, new TargetPerception(a.Id, a.StaminaPct, 0, 0, 0)));
                    }

                    break;
                default:
                    targets.Add((null, null));
                    break;
            }

            foreach (var (targetId, target) in targets)
            {
                var baseScore = profile.BaseScores[actionType];
                var factorContribution = 0.0;
                foreach (var factor in ScoringFactors.All)
                {
                    var scores = factor.Evaluate(perception, target);
                    var weight = profile.FactorWeights[factor.Name];
                    factorContribution += weight * scores.For(actionType);
                }

                candidates.Add(new ScoredCandidate(actionType, targetId, baseScore + (factorContribution * coeff)));
            }
        }

        if (candidates.Count == 0)
        {
            return new CombatAction(combatant.Id, ActionType.Evade, null, null);
        }

        var sorted = candidates.OrderByDescending(c => c.Score).ToList();
        var topScore = sorted[0].Score;
        var tied = sorted.Where(c => c.Score == topScore).ToList();

        var staminaByTarget = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var e in perception.Enemies)
        {
            staminaByTarget[e.Id] = e.StaminaPct;
        }

        foreach (var a in perception.Allies)
        {
            staminaByTarget[a.Id] = a.StaminaPct;
        }

        var winner = tied.Count == 1 ? tied[0] : TieBreaking.ResolveTie(tied, combatant.ElementalPath, staminaByTarget);

        return new CombatAction(
            combatant.Id,
            winner.ActionType,
            winner.TargetId,
            winner.ActionType == ActionType.Special ? combatant.Energy : null);
    }
}
