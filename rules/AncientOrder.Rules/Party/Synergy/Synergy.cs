using System.Collections.Immutable;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Party;

public static class Synergy
{
    /// <summary>Synergy for the player plus the current lineup (in lineup order), worked out on demand. No lineup means no bonus.</summary>
    public static ImmutableArray<SynergyResult> Current(GameContent content, GameState state)
    {
        var lineup = CharacterRules.InLineup(content, state);
        if (lineup.Length == 0)
        {
            return ImmutableArray<SynergyResult>.Empty;
        }

        return SynergyCalculator.Evaluate(
            content.Synergy,
            state.Player.Personality,
            lineup.Select(v => v.Definition.Personality).ToImmutableArray());
    }
}
