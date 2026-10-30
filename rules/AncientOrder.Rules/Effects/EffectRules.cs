using System.Collections.Immutable;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Conversations;
using AncientOrder.Rules.HandOff;
using AncientOrder.Rules.Party;
using AncientOrder.Rules.Personality;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Effects;

/// <summary>What applying a list of effects produced: the new game and the report lists the effects fill.</summary>
public sealed record AppliedEffects(GameState Game, ImmutableArray<RelationshipChange> RelationshipChanges)
{
    public ImmutableArray<TraitChange> TraitChanges { get; init; } = [];

    public ImmutableArray<TraitShift> WrittenShift { get; init; } = [];

    public ImmutableArray<string> Joined { get; init; } = [];

    public ImmutableArray<string> Left { get; init; } = [];

    public ImmutableArray<string> FlagsSet { get; init; } = [];

    public ImmutableArray<string> FlagsCleared { get; init; } = [];

    public string? HandOff { get; init; }
}

public static class EffectRules
{
    /// <summary>Applies the effects in written order. The first one refused refuses all of them; the given game is never changed.</summary>
    public static Result<AppliedEffects> Apply(GameContent content, GameState game, ImmutableArray<Effect> effects, long now)
    {
        var personalityBefore = game.Player.Personality;
        var written = new double[PersonalityRules.Traits.Length];
        var wrote = new bool[written.Length];
        var relationships = ImmutableArray.CreateBuilder<RelationshipChange>();
        var joined = ImmutableArray.CreateBuilder<string>();
        var left = ImmutableArray.CreateBuilder<string>();
        var flagsSet = ImmutableArray.CreateBuilder<string>();
        var flagsCleared = ImmutableArray.CreateBuilder<string>();
        string? handOff = null;
        foreach (var effect in effects)
        {
            switch (effect)
            {
                case Effect.Relationship r:
                    var before = game.Characters.TryGetValue(r.CharacterId, out var was) ? was : null;
                    var changed = CharacterRules.ChangeRelationship(content, game, r.CharacterId, r.Affection ?? 0, r.Trust ?? 0, now);
                    if (!changed.IsOk)
                    {
                        return Result<AppliedEffects>.Fail(changed.Error);
                    }

                    game = changed.Value;
                    var after = game.Characters[r.CharacterId];
                    relationships.Add(new RelationshipChange(r.CharacterId, after.Affection - before!.Affection, after.Trust - before.Trust));
                    break;
                case Effect.Join j:
                    var wasIn = game.Party.Contains(j.CharacterId, StringComparer.Ordinal);
                    var joining = PartyRules.Join(content, game, j.CharacterId, now);
                    if (!joining.IsOk)
                    {
                        return Result<AppliedEffects>.Fail(joining.Error);
                    }

                    game = joining.Value;
                    if (!wasIn)
                    {
                        joined.Add(j.CharacterId);
                    }

                    break;
                case Effect.Leave l:
                    var wasMember = game.Party.Contains(l.CharacterId, StringComparer.Ordinal);
                    var leaving = PartyRules.Leave(content, game, l.CharacterId, now);
                    if (!leaving.IsOk)
                    {
                        return Result<AppliedEffects>.Fail(leaving.Error);
                    }

                    game = leaving.Value;
                    if (wasMember)
                    {
                        left.Add(l.CharacterId);
                    }

                    break;
                case Effect.Shift shift:
                    game = GameStateUpdates.AdjustPlayerPersonality(game, shift.Adjustment, now);
                    foreach (var (trait, delta) in shift.Adjustment.Deltas)
                    {
                        written[(int)trait] += delta;
                        wrote[(int)trait] = true;
                    }

                    break;
                case Effect.SetFlag s:
                    if (!game.Memory.HasFlag(s.FlagId))
                    {
                        game = game with { Memory = game.Memory.WithFlag(s.FlagId), Timestamp = now };
                        flagsSet.Add(s.FlagId);
                    }

                    break;
                case Effect.ClearFlag c:
                    if (game.Memory.HasFlag(c.FlagId))
                    {
                        game = game with { Memory = game.Memory.WithoutFlag(c.FlagId), Timestamp = now };
                        flagsCleared.Add(c.FlagId);
                    }

                    break;
                case Effect.HandOff h:
                    var handingOff = HandOffRules.Leave(content, game, h.FightId, now);
                    if (!handingOff.IsOk)
                    {
                        return Result<AppliedEffects>.Fail(handingOff.Error);
                    }

                    game = handingOff.Value;
                    handOff = h.FightId;
                    break;
                default:
                    throw new InvalidOperationException($"Unknown effect {effect.GetType().Name}");
            }
        }

        var traitChanges = ImmutableArray.CreateBuilder<TraitChange>();
        var writtenShift = ImmutableArray.CreateBuilder<TraitShift>();
        foreach (var trait in PersonalityRules.Traits)
        {
            var was = personalityBefore[trait];
            var became = game.Player.Personality[trait];
            if (was != became)
            {
                traitChanges.Add(new TraitChange(trait, was, became));
            }

            if (wrote[(int)trait])
            {
                writtenShift.Add(new TraitShift(trait, written[(int)trait]));
            }
        }

        return Result<AppliedEffects>.Ok(new AppliedEffects(game, relationships.ToImmutable()) { TraitChanges = traitChanges.ToImmutable(), WrittenShift = writtenShift.ToImmutable(), Joined = joined.ToImmutable(), Left = left.ToImmutable(), FlagsSet = flagsSet.ToImmutable(), FlagsCleared = flagsCleared.ToImmutable(), HandOff = handOff });
    }
}
