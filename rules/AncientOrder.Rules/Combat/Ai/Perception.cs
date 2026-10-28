using System.Collections.Immutable;

namespace AncientOrder.Rules.Combat.Ai;

public sealed record AllyPerception(string Id, double StaminaPct, bool IsKO);

public sealed record EnemyPerception(string Id, double StaminaPct, bool IsKO, double SpeedDelta, double RankDelta, double Power);

/// <summary>What a factor sees of one candidate target. Allies carry neutral speed, rank, and power values.</summary>
public sealed record TargetPerception(string Id, double StaminaPct, double SpeedDelta, double RankDelta, double Power);

/// <summary>Pre-computed snapshot of the fight from one combatant's side. Allies (self excluded) and enemies include knocked-out ones, sorted weakest first.</summary>
public sealed record CombatPerception(
    string SelfId,
    double SelfStaminaPct,
    double SelfEnergy,
    AscensionLevel SelfAscension,
    double SelfRank,
    ElementalPath SelfPath,
    ImmutableArray<AllyPerception> Allies,
    double LowestAllyStaminaPct,
    double TeamAvgStaminaPct,
    int AllyCount,
    ImmutableArray<EnemyPerception> Enemies,
    double WeakestEnemyStaminaPct,
    double EnemyAvgStaminaPct,
    int EnemyCount,
    int Round);

public static class Perception
{
    public static CombatPerception Build(Combatant combatant, CombatState state)
    {
        var isInPlayerParty = state.PlayerParty.Any(c => c.Id == combatant.Id);
        var ownParty = isInPlayerParty ? state.PlayerParty : state.EnemyParty;
        var opposingParty = isInPlayerParty ? state.EnemyParty : state.PlayerParty;

        var selfStaminaPct = combatant.MaxStamina > 0 ? combatant.Stamina / combatant.MaxStamina : 0;

        var allAllies = ownParty
            .Where(c => c.Id != combatant.Id)
            .Select(c => new AllyPerception(c.Id, c.MaxStamina > 0 ? c.Stamina / c.MaxStamina : 0, c.IsKO))
            .OrderBy(a => a.StaminaPct)
            .ToImmutableArray();

        var nonKoAllies = allAllies.Where(a => !a.IsKO).ToImmutableArray();
        var allyCount = nonKoAllies.Length;

        var lowestAllyStaminaPct = 1.0;
        if (nonKoAllies.Length > 0)
        {
            lowestAllyStaminaPct = nonKoAllies[0].StaminaPct;
            foreach (var a in nonKoAllies)
            {
                lowestAllyStaminaPct = Math.Min(lowestAllyStaminaPct, a.StaminaPct);
            }
        }

        var teamSum = 0.0;
        foreach (var a in nonKoAllies)
        {
            teamSum += a.StaminaPct;
        }

        teamSum += selfStaminaPct;
        var teamAvgStaminaPct = teamSum / (nonKoAllies.Length + 1);

        var allEnemies = opposingParty
            .Select(e => new EnemyPerception(
                e.Id,
                e.MaxStamina > 0 ? e.Stamina / e.MaxStamina : 0,
                e.IsKO,
                e.Speed > 0 ? (combatant.Speed - e.Speed) / e.Speed : 0,
                combatant.Rank - e.Rank,
                e.Power))
            .OrderBy(e => e.StaminaPct)
            .ToImmutableArray();

        var nonKoEnemies = allEnemies.Where(e => !e.IsKO).ToImmutableArray();
        var enemyCount = nonKoEnemies.Length;

        var weakestEnemyStaminaPct = 1.0;
        var enemyAvgStaminaPct = 0.0;
        if (nonKoEnemies.Length > 0)
        {
            weakestEnemyStaminaPct = nonKoEnemies[0].StaminaPct;
            var enemySum = 0.0;
            foreach (var e in nonKoEnemies)
            {
                weakestEnemyStaminaPct = Math.Min(weakestEnemyStaminaPct, e.StaminaPct);
                enemySum += e.StaminaPct;
            }

            enemyAvgStaminaPct = enemySum / nonKoEnemies.Length;
        }

        return new CombatPerception(
            combatant.Id,
            selfStaminaPct,
            combatant.Energy,
            combatant.AscensionLevel,
            combatant.Rank,
            combatant.ElementalPath,
            allAllies,
            lowestAllyStaminaPct,
            teamAvgStaminaPct,
            allyCount,
            allEnemies,
            weakestEnemyStaminaPct,
            enemyAvgStaminaPct,
            enemyCount,
            state.Round);
    }
}
