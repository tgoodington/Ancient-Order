using System.Collections.Immutable;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Party;

/// <summary>The player's readout: name, the six personality values, and combat stats if the player fights.</summary>
public sealed record PlayerReadout(string Name, AncientOrder.Rules.Personality.Personality Personality, CombatProfile? Combat);

/// <summary>A party member's readout, shown only when the pack's display settings allow it.</summary>
public sealed record MemberReadout(AncientOrder.Rules.Personality.Personality Personality, CombatProfile? Combat);

/// <summary>One party member. CanFight means the character has a combat profile. Readout is null when readouts are off.</summary>
public sealed record PartyMemberView(string Id, string Name, bool CanFight, bool InLineup, MemberReadout? Readout);

public sealed record LineupMemberView(string Id, string Name);

/// <summary>
/// One active bonus. Feel is the content's line (or null). Name, Stat, Step, and Value are filled only when the pack's
/// display settings show synergy numbers.
/// </summary>
public sealed record PartySynergyView(string? Feel, string? Name, CombatStat? Stat, StatStep? Step, double? Value);

/// <summary>
/// Everything the party screen shows, ready made. Only the player and party members are described: never enemies or other
/// characters, and never affection or trust.
/// </summary>
public sealed record PartyView(
    PlayerReadout Player,
    ImmutableArray<PartyMemberView> Members,
    ImmutableArray<LineupMemberView> Lineup,
    LineupLimits Limits,
    LineupReadiness Readiness,
    ImmutableArray<PartySynergyView> Synergy,
    bool ShowMemberReadouts,
    bool ShowSynergyNumbers)
{
    public static PartyView From(GameContent content, GameState game)
    {
        var display = content.Settings.Display;
        var lineupIds = game.Lineup.IsDefault ? ImmutableArray<string>.Empty : game.Lineup;
        var player = new PlayerReadout(game.Player.Name, game.Player.Personality, content.Settings.PlayerCombat);

        var members = ImmutableArray.CreateBuilder<PartyMemberView>();
        foreach (var id in game.Party)
        {
            if (content.Characters.TryGet(id, out var definition))
            {
                members.Add(new PartyMemberView(
                    id,
                    definition.Name,
                    definition.Combat is not null,
                    lineupIds.Contains(id, StringComparer.Ordinal),
                    display.PartyReadouts ? new MemberReadout(definition.Personality, definition.Combat) : null));
            }
        }

        var lineup = ImmutableArray.CreateBuilder<LineupMemberView>();
        foreach (var id in lineupIds)
        {
            if (content.Characters.TryGet(id, out var definition))
            {
                lineup.Add(new LineupMemberView(id, definition.Name));
            }
        }

        var limits = content.Settings.Lineup;
        var synergy = Party.Synergy.Current(content, game)
            .Select(r => display.SynergyNumbers
                ? new PartySynergyView(r.Feel, r.Name, r.Stat, r.Step, r.Value)
                : new PartySynergyView(r.Feel, null, null, null, null))
            .ToImmutableArray();

        return new PartyView(
            player,
            members.ToImmutable(),
            lineup.ToImmutable(),
            limits,
            LineupRules.Ready(content, game, limits),
            synergy,
            display.PartyReadouts,
            display.SynergyNumbers);
    }
}
