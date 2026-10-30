using System.Collections.Immutable;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Conversations;
using AncientOrder.Rules.Personality;
using AncientOrder.Rules.Town;

namespace AncientOrder.Rules.State;
/// <summary>Every update returns a new state stamped with now.</summary>
public static class GameStateUpdates
{
    public static GameState NewGame(GameContent content, string playerId, long now)
    {
        var characters = ImmutableDictionary.CreateBuilder<string, CharacterState>(StringComparer.Ordinal);
        foreach (var definition in content.Characters.Definitions)
        {
            characters[definition.Id] = new CharacterState(definition.StartingAffection, definition.StartingTrust);
        }

        return new GameState(
            new PlayerCharacter(
                playerId,
                content.Settings.PlayerName,
                content.Settings.StartingPersonality ?? PersonalityRules.CreateDefault()),
            characters.ToImmutable(),
            ImmutableArray<string>.Empty,
            null,
            ImmutableDictionary<string, ReactionProgress>.Empty.WithComparers(StringComparer.Ordinal),
            now)
        {
            Party = content.Settings.StartingParty.IsDefault ? [] : content.Settings.StartingParty,
            Town = content.Towns.TryGet(content.Settings.StartingTown, out var town) ? new TownPresence(town.Id, town.Start) : null,
        };
    }

    public static GameState SetPlayerPersonality(GameState state, AncientOrder.Rules.Personality.Personality personality, long now) =>
        state with { Player = state.Player with { Personality = personality }, Timestamp = now };

    public static GameState AdjustPlayerPersonality(GameState state, PersonalityAdjustment adjustment, long now) =>
        SetPlayerPersonality(state, PersonalityRules.Adjust(state.Player.Personality, adjustment), now);

    public static GameState WithCombat(GameState state, CombatState? combat, long now) =>
        state with { Combat = combat, Timestamp = now };

    public static GameState WithConversation(GameState state, ActiveConversation? conversation, long now) =>
        state with { Conversation = conversation, Timestamp = now };
}
