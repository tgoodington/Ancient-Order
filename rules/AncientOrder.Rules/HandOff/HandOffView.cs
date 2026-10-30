using System.Collections.Immutable;
using AncientOrder.Rules.Party;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.HandOff;

/// <summary>The hand-off ready to show on the placeholder screen: the fight's id and name, and the lineup's ids and names in order.</summary>
public sealed record HandOffView(string FightId, string FightName, ImmutableArray<LineupMemberView> Lineup)
{
    /// <summary>Derives the view, or null when no hand-off is recorded. A fight or character the content no longer has shows by id.</summary>
    public static HandOffView? From(GameContent content, GameState game)
    {
        if (game.HandOff is not { } handOff)
        {
            return null;
        }

        var name = content.Encounters.TryGet(handOff.FightId, out var fight) ? fight.Name : handOff.FightId;
        var lineup = handOff.Lineup.IsDefault
            ? []
            : handOff.Lineup.Select(id => new LineupMemberView(id, content.Characters.TryGet(id, out var c) ? c.Name : id)).ToImmutableArray();
        return new HandOffView(handOff.FightId, name, lineup);
    }
}
