using System.Collections.Immutable;
using AncientOrder.Rules.Saves.Format;
using AncientOrder.Rules.State;
using CombatReactionProgress = AncientOrder.Rules.Combat.ReactionProgress;

namespace AncientOrder.Rules.Saves;

public enum LoadAdjustmentKind
{
    CharacterDropped,
    CharacterAdded,
    LineupCleared,
}

/// <summary>One change made to fit a save to the current content. Id is the character concerned, or null for the lineup.</summary>
public sealed record LoadAdjustment(LoadAdjustmentKind Kind, string? Id, string Message);

public sealed record LoadReport(ImmutableArray<LoadAdjustment> Adjustments)
{
    public static LoadReport Empty { get; } = new(ImmutableArray<LoadAdjustment>.Empty);

    public bool IsEmpty => Adjustments.IsEmpty;
}

public sealed record Reconciled(GameState Game, LoadReport Report);

/// <summary>Pairs a sound saved game with the current content. It never refuses.</summary>
public static class SaveReconciler
{
    public static Reconciled Reconcile(SavedGame saved, GameContent content)
    {
        var adjustments = ImmutableArray.CreateBuilder<LoadAdjustment>();
        var savedById = saved.Characters.ToDictionary(c => c.Id, StringComparer.Ordinal);

        var characters = ImmutableDictionary.CreateBuilder<string, CharacterState>(StringComparer.Ordinal);
        foreach (var definition in content.Characters.Definitions)
        {
            if (savedById.TryGetValue(definition.Id, out var kept))
            {
                characters[definition.Id] = new CharacterState(kept.Affection, kept.Trust);
            }
            else
            {
                characters[definition.Id] = new CharacterState(definition.StartingAffection, definition.StartingTrust);
                adjustments.Add(new LoadAdjustment(
                    LoadAdjustmentKind.CharacterAdded,
                    definition.Id,
                    $"{definition.Id} is new in the content and starts at its starting relationship."));
            }
        }

        foreach (var entry in saved.Characters)
        {
            if (!content.Characters.TryGet(entry.Id, out _))
            {
                adjustments.Add(new LoadAdjustment(
                    LoadAdjustmentKind.CharacterDropped,
                    entry.Id,
                    $"{entry.Id} is no longer in the content and was dropped."));
            }
        }

        var lineup = saved.Lineup;
        var lineupReason = LineupProblem(lineup, content);
        if (lineupReason is not null)
        {
            lineup = ImmutableArray<string>.Empty;
            adjustments.Add(new LoadAdjustment(LoadAdjustmentKind.LineupCleared, null, "The lineup was cleared: " + lineupReason + "."));
        }

        // Kept even for ids not in the content, so the progress returns if the character comes back.
        var progress = ImmutableDictionary.CreateBuilder<string, CombatReactionProgress>(StringComparer.Ordinal);
        foreach (var entry in saved.ReactionProgress)
        {
            progress[entry.Id] = SaveMapper.ToProgress(entry.Block, entry.Dodge, entry.Parry);
        }

        var game = new GameState(
            SaveMapper.ToPlayer(saved.Player),
            characters.ToImmutable(),
            lineup,
            saved.Combat is null ? null : SaveMapper.ToCombat(saved.Combat),
            progress.ToImmutable(),
            saved.Timestamp);

        return new Reconciled(game, new LoadReport(adjustments.ToImmutable()));
    }

    private static string? LineupProblem(ImmutableArray<string> lineup, GameContent content)
    {
        if (lineup.IsEmpty)
        {
            return null;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in lineup)
        {
            if (!content.Characters.TryGet(id, out var definition))
            {
                return $"{id} is no longer in the content";
            }

            if (definition.Combat is null)
            {
                return $"{id} has no combat stats";
            }

            if (!seen.Add(id))
            {
                return $"{id} appears more than once";
            }
        }

        if (lineup.Length != content.Settings.LineupSize)
        {
            return "the lineup size has changed";
        }

        return null;
    }
}
