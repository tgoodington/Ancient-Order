using System.Collections.Immutable;
using AncientOrder.Rules.Common;

namespace AncientOrder.Rules.State;

/// <summary>
/// The characters module: the only code that reads or changes characters.
/// A character missing from either the catalog or the game's character states counts as unknown.
/// </summary>
public static class CharacterRules
{
    public static Result<CharacterView> Get(GameContent content, GameState state, string id) =>
        TryView(content, state, id, out var view)
            ? Result<CharacterView>.Ok(view)
            : Result<CharacterView>.Fail(NotFound(id));

    public static ImmutableArray<CharacterView> All(GameContent content, GameState state)
    {
        var views = ImmutableArray.CreateBuilder<CharacterView>();
        foreach (var definition in content.Characters.Definitions)
        {
            if (TryView(content, state, definition.Id, out var view))
            {
                views.Add(view);
            }
        }

        return views.ToImmutable();
    }

    public static ImmutableArray<CharacterView> InLineup(GameContent content, GameState state)
    {
        var views = ImmutableArray.CreateBuilder<CharacterView>();
        foreach (var id in state.Lineup)
        {
            if (TryView(content, state, id, out var view))
            {
                views.Add(view);
            }
        }

        return views.ToImmutable();
    }

    public static Result<GameState> ChangeAffection(GameContent content, GameState state, string id, double change, long now) =>
        Change(content, state, id, change, 0, now, changeAffection: true, changeTrust: false);

    public static Result<GameState> ChangeTrust(GameContent content, GameState state, string id, double change, long now) =>
        Change(content, state, id, 0, change, now, changeAffection: false, changeTrust: true);

    public static Result<GameState> ChangeRelationship(
        GameContent content, GameState state, string id, double affectionChange, double trustChange, long now) =>
        Change(content, state, id, affectionChange, trustChange, now, changeAffection: true, changeTrust: true);

    private static Result<GameState> Change(
        GameContent content,
        GameState state,
        string id,
        double affectionChange,
        double trustChange,
        long now,
        bool changeAffection,
        bool changeTrust)
    {
        if (!TryView(content, state, id, out var view))
        {
            return Result<GameState>.Fail(NotFound(id));
        }

        // Deliberate difference from the TypeScript, which stored NaN/Infinity changes.
        if (!double.IsFinite(affectionChange) || !double.IsFinite(trustChange))
        {
            return Result<GameState>.Fail(new RuleError(ErrorCode.VALIDATION_ERROR, "Relationship change must be a finite number"));
        }

        var current = view.State;
        var updated = current with
        {
            Affection = changeAffection ? Clamp(current.Affection + affectionChange) : current.Affection,
            Trust = changeTrust ? Clamp(current.Trust + trustChange) : current.Trust,
        };

        return Result<GameState>.Ok(state with { Characters = state.Characters.SetItem(id, updated), Timestamp = now });
    }

    private static double Clamp(double value) => Math.Max(-100, Math.Min(100, value));

    private static bool TryView(GameContent content, GameState state, string id, out CharacterView view)
    {
        if (content.Characters.TryGet(id, out var definition) && state.Characters.TryGetValue(id, out var characterState))
        {
            view = new CharacterView(definition, characterState);
            return true;
        }

        view = null!;
        return false;
    }

    private static RuleError NotFound(string id) => new(ErrorCode.NPC_NOT_FOUND, $"NPC not found: \"{id}\"");
}
