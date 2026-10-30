using System.Collections.Immutable;
using AncientOrder.Rules.Conversations;
using AncientOrder.Rules.HandOff;
using AncientOrder.Rules.Party;
using AncientOrder.Rules.Saves.Format;
using AncientOrder.Rules.State;
using AncientOrder.Rules.Town;
using CombatReactionProgress = AncientOrder.Rules.Combat.ReactionProgress;

namespace AncientOrder.Rules.Saves;

public enum LoadAdjustmentKind
{
    CharacterDropped,
    CharacterAdded,
    LineupCleared,
    PartyMemberDropped,
    LineupMemberDropped,
    TownReset,
    PositionReset,
    ConversationEnded,
    ConversationRestarted,
    HandOffCleared,
    HandOffTownCleared,
}

/// <summary>One change made to fit a save to the current content. Id is the character, town, conversation, or fight concerned, or null for the lineup as a whole.</summary>
public sealed record LoadAdjustment(LoadAdjustmentKind Kind, string? Id, string Message);

public sealed record LoadReport(ImmutableArray<LoadAdjustment> Adjustments)
{
    public static LoadReport Empty { get; } = new(ImmutableArray<LoadAdjustment>.Empty);

    public bool IsEmpty => Adjustments.IsEmpty;
}

public sealed record Reconciled(GameState Game, LoadReport Report);

/// <summary>
/// Pairs a sound saved game with the current content. It never refuses. Steps run in order (characters, party, lineup, town,
/// conversation, hand-off) so later steps see earlier fixes. Story memory is kept whole, even ids the content no longer uses.
/// </summary>
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

        var party = ImmutableArray.CreateBuilder<string>();
        foreach (var id in saved.Party)
        {
            if (content.Characters.TryGet(id, out _))
            {
                party.Add(id);
            }
            else
            {
                adjustments.Add(new LoadAdjustment(
                    LoadAdjustmentKind.PartyMemberDropped,
                    id,
                    $"{id} is no longer in the content and left the party."));
            }
        }

        var lineup = ReconcileLineup(saved.Lineup, party, content, adjustments);

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
            saved.Timestamp)
        {
            Party = party.ToImmutable(),
            Memory = SaveMapper.ToMemory(saved.Memory),
        };

        game = game with { Town = ReconcileTown(saved.Town, content, adjustments) };
        game = game with { Conversation = ReconcileConversation(saved.Conversation, content, game, adjustments) };
        game = game with { HandOff = ReconcileHandOff(saved.HandOff, content, game, adjustments) };

        // A conversation cannot run during a fight or after leaving for one (a fight with a hand-off together is fine: the fight starts from it).
        if (game.Conversation is { } running && (game.Combat is not null || game.HandOff is not null))
        {
            adjustments.Add(new LoadAdjustment(
                LoadAdjustmentKind.ConversationEnded,
                running.ConversationId,
                $"Conversation {running.ConversationId} cannot run during a fight or after leaving for one, so it was ended."));
            game = game with { Conversation = null };
        }

        return new Reconciled(game, new LoadReport(adjustments.ToImmutable()));
    }

    private static ImmutableArray<string> ReconcileLineup(
        ImmutableArray<string> saved, ImmutableArray<string>.Builder party, GameContent content, ImmutableArray<LoadAdjustment>.Builder adjustments)
    {
        var kept = ImmutableArray.CreateBuilder<string>();
        foreach (var id in saved)
        {
            var problem = !content.Characters.TryGet(id, out var definition)
                ? "is no longer in the content"
                : !party.Contains(id, StringComparer.Ordinal)
                    ? "is not in the party"
                    : definition.Combat is null
                        ? "has no combat stats"
                        : kept.Contains(id, StringComparer.Ordinal) ? "appears more than once" : null;
            if (problem is null)
            {
                kept.Add(id);
            }
            else
            {
                adjustments.Add(new LoadAdjustment(LoadAdjustmentKind.LineupMemberDropped, id, $"{id} {problem} and was removed from the lineup."));
            }
        }

        if (kept.Count > content.Settings.Lineup.Max)
        {
            adjustments.Add(new LoadAdjustment(LoadAdjustmentKind.LineupCleared, null, "The lineup was cleared: the lineup is larger than the content allows."));
            return ImmutableArray<string>.Empty;
        }

        return kept.ToImmutable();
    }

    private static TownPresence? ReconcileTown(SavedTown? saved, GameContent content, ImmutableArray<LoadAdjustment>.Builder adjustments)
    {
        var start = content.Towns.TryGet(content.Settings.StartingTown, out var startingTown) ? startingTown : null;
        if (saved is null || !content.Towns.TryGet(saved.Town, out var town))
        {
            if (saved is null && start is null)
            {
                return null;
            }

            var message = saved is null
                ? $"The save has no town, so the player starts at the start of {start!.Id}."
                : start is null
                    ? $"Town {saved.Town} is no longer in the content and the content has no starting town."
                    : $"Town {saved.Town} is no longer in the content, so the player returned to the start of {start.Id}.";
            adjustments.Add(new LoadAdjustment(LoadAdjustmentKind.TownReset, saved?.Town ?? start?.Id, message));
            return start is null ? null : new TownPresence(start.Id, start.Start);
        }

        var position = new Point(saved.X, saved.Z);
        if (!TownRules.Ground(content, town.Id).IsWalkable(position))
        {
            adjustments.Add(new LoadAdjustment(
                LoadAdjustmentKind.PositionReset,
                town.Id,
                $"The saved position is not walkable in {town.Id}, so the player moved to its start."));
            return new TownPresence(town.Id, town.Start);
        }

        return new TownPresence(town.Id, position);
    }

    private static ActiveConversation? ReconcileConversation(
        SavedConversation? saved, GameContent content, GameState game, ImmutableArray<LoadAdjustment>.Builder adjustments)
    {
        if (saved is null)
        {
            return null;
        }

        if (!content.Conversations.TryGet(saved.Conversation, out var conversation))
        {
            adjustments.Add(new LoadAdjustment(
                LoadAdjustmentKind.ConversationEnded,
                saved.Conversation,
                $"Conversation {saved.Conversation} is no longer in the content and was ended."));
            return null;
        }

        if (conversation.Steps.Any(step => step.Id == saved.Step))
        {
            return new ActiveConversation(conversation.Id, saved.Step);
        }

        var opening = ConversationRules.OpeningStep(content, game, conversation);
        adjustments.Add(new LoadAdjustment(
            LoadAdjustmentKind.ConversationRestarted,
            saved.Conversation,
            $"Step {saved.Step} of {saved.Conversation} is no longer in the content, so the conversation restarted at {opening}."));
        return new ActiveConversation(conversation.Id, opening);
    }

    private static FightHandOff? ReconcileHandOff(
        SavedHandOff? saved, GameContent content, GameState game, ImmutableArray<LoadAdjustment>.Builder adjustments)
    {
        if (saved is null)
        {
            return null;
        }

        string? problem = null;
        if (!content.Encounters.TryGet(saved.Fight, out _))
        {
            problem = "is no longer in the content";
        }
        else
        {
            var readiness = LineupRules.Ready(content, game with { Lineup = saved.Lineup }, HandOffRules.LimitsFor(content, saved.Fight));
            if (!readiness.Ready)
            {
                problem = "is no longer ready: " + readiness.Reason;
            }
        }

        if (problem is null)
        {
            var townId = saved.Town;
            if (townId is not null && !content.Towns.TryGet(townId, out _))
            {
                adjustments.Add(new LoadAdjustment(
                    LoadAdjustmentKind.HandOffTownCleared,
                    townId,
                    $"The hand-off named town {townId}, which is no longer in the content, so it names no town."));
                townId = null;
            }

            return new FightHandOff(saved.Fight, saved.Lineup, townId);
        }

        adjustments.Add(new LoadAdjustment(
            LoadAdjustmentKind.HandOffCleared,
            saved.Fight,
            $"The hand-off to {saved.Fight} {problem}, so the player is back in the town."));
        return null;
    }
}
