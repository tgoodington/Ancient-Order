using System.Collections.Immutable;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Party;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Session;

internal static class CharacterOperations
{
    /// <summary>The live game's characters when a game is active, else the content's starting values. Catalog order.</summary>
    public static Outcome<ImmutableArray<CharacterView>> ListCharacters(SessionContext context, SessionSnapshot s)
    {
        if (s.Game is not null)
        {
            return Outcome.Read(s, CharacterRules.All(context.Content, s.Game));
        }

        return Outcome.Read(s, context.Content.Characters.Definitions.Select(StartingView).ToImmutableArray());
    }

    public static Outcome<CharacterView> GetCharacter(SessionContext context, SessionSnapshot s, string? id)
    {
        var invalid = SessionValidation.IdString("id", id);
        if (invalid is not null)
        {
            return Outcome.Refuse<CharacterView>(s, invalid);
        }

        if (s.Game is not null)
        {
            var live = CharacterRules.Get(context.Content, s.Game, id!);
            return live.IsOk ? Outcome.Read(s, live.Value) : Outcome.Refuse<CharacterView>(s, live.Error);
        }

        return context.Content.Characters.TryGet(id!, out var definition)
            ? Outcome.Read(s, StartingView(definition))
            : Outcome.Refuse<CharacterView>(s, ErrorCode.NPC_NOT_FOUND, $"NPC not found: \"{id}\"");
    }

    public static Outcome<ImmutableArray<SynergyResult>> GetSynergy(SessionContext context, SessionSnapshot s) =>
        s.Game is null ? Outcome.NoActiveGame<ImmutableArray<SynergyResult>>(s) : Outcome.Read(s, s.Synergy);

    private static CharacterView StartingView(CharacterDefinition definition) =>
        new(definition, new CharacterState(definition.StartingAffection, definition.StartingTrust));
}
