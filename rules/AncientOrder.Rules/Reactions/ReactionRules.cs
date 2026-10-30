using System.Collections.Immutable;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Conversations;
using AncientOrder.Rules.Personality;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Reactions;

/// <summary>The game after the characters present reacted, and each reaction in participant order.</summary>
public sealed record ReactionOutcome(GameState Game, ImmutableArray<ReactionReport> Reactions);

/// <summary>
/// How the characters present react to a choice. Participants are the conversation's, in order; each reacts at most once.
/// Deterministic: traits in trait order, participants in participant order, no map iteration.
/// </summary>
public static class ReactionRules
{
    public static Result<ReactionOutcome> React(
        GameContent content,
        GameState game,
        ConversationDefinition conversation,
        ChoiceDefinition choice,
        ImmutableArray<TraitShift> writtenShift,
        long now)
    {
        var reactions = ImmutableArray.CreateBuilder<ReactionReport>();
        if (conversation.Participants.IsDefault)
        {
            return Result<ReactionOutcome>.Ok(new ReactionOutcome(game, []));
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in conversation.Participants)
        {
            if (!content.Characters.TryGet(id, out var character) || !seen.Add(id))
            {
                continue;
            }

            var over = choice.Reactions.IsDefault ? null : choice.Reactions.FirstOrDefault(o => o.CharacterId == id);
            if (over is { Silence: true })
            {
                continue;
            }

            var tell = over is null ? FromArchetype(content, character, writtenShift) : Written(content, character, over);
            if (tell is null)
            {
                continue;
            }

            var before = game.Characters.TryGetValue(id, out var was) ? was : null;
            var changed = CharacterRules.ChangeRelationship(content, game, id, tell.IntendedAffection, tell.IntendedTrust, now);
            if (!changed.IsOk)
            {
                return Result<ReactionOutcome>.Fail(changed.Error);
            }

            game = changed.Value;
            var after = game.Characters[id];
            reactions.Add(tell with { AffectionChange = after.Affection - before!.Affection, TrustChange = after.Trust - before.Trust });
        }

        return Result<ReactionOutcome>.Ok(new ReactionOutcome(game, reactions.ToImmutable()));
    }

    private static ReactionReport? FromArchetype(GameContent content, CharacterDefinition character, ImmutableArray<TraitShift> writtenShift)
    {
        if (writtenShift.IsDefaultOrEmpty
            || !content.Archetypes.TryGet(character.ArchetypeId, out var archetype)
            || archetype.Reactions is not { } profile)
        {
            return null;
        }

        var score = 0.0;
        foreach (var trait in PersonalityRules.Traits)
        {
            foreach (var shift in writtenShift)
            {
                if (shift.Trait == trait)
                {
                    score += profile.Leanings.Of(trait) * shift.Amount;
                }
            }
        }

        if (Math.Abs(score) < profile.Minimum)
        {
            return null;
        }

        var affection = Limit(JsMath.Round(score * profile.AffectionPerPoint));
        var trust = Limit(JsMath.Round(score * profile.TrustPerPoint));
        if (affection == 0 && trust == 0)
        {
            return null;
        }

        var strength = StrengthOf(content, affection, trust);
        var approve = score > 0;
        var lines = approve ? profile.Approve : profile.Disapprove;
        return Build(character, approve ? ReactionKind.Approve : ReactionKind.Disapprove, DirectionOf(affection, trust), strength, lines.For(strength), affection, trust);
    }

    private static ReactionReport Written(GameContent content, CharacterDefinition character, ReactionOverride over)
    {
        var affection = Limit(over.Affection ?? 0);
        var trust = Limit(over.Trust ?? 0);
        var direction = DirectionOf(affection, trust);
        var strength = direction == TellDirection.Neutral ? TellStrength.Slight : StrengthOf(content, affection, trust);
        return Build(character, ReactionKind.Written, direction, strength, over.Text ?? string.Empty, affection, trust);
    }

    private static ReactionReport Build(CharacterDefinition character, ReactionKind kind, TellDirection direction, TellStrength strength, string text, double affection, double trust) =>
        new(character.Id, character.Name, kind, direction, strength, text.Replace("{name}", character.Name, StringComparison.Ordinal), affection, trust, 0, 0);

    // Intended changes are limited to +-200: anything larger already saturates the -100..100 relationship range, and an unlimited product of finite content values could overflow and refuse the choice. The report shows the limited numbers.
    private static double Limit(double change) => double.IsNaN(change) ? 0 : Math.Clamp(change, -200, 200);

    private static TellDirection DirectionOf(double affection, double trust)
    {
        var sum = affection + trust;
        return sum > 0 ? TellDirection.Warmer : sum < 0 ? TellDirection.Cooler : TellDirection.Neutral;
    }

    private static TellStrength StrengthOf(GameContent content, double affection, double trust)
    {
        var size = Math.Max(Math.Abs(affection), Math.Abs(trust));
        var tells = content.Settings.Tells;
        return size < tells.ClearAt ? TellStrength.Slight : size >= tells.StrongAt ? TellStrength.Strong : TellStrength.Clear;
    }
}
