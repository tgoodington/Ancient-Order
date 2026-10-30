using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Conditions;
using AncientOrder.Rules.Effects;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Conversations;

/// <summary>The conversations in content order plus a lookup by id.</summary>
public sealed record ConversationCatalog
{
    private ConversationCatalog(ImmutableArray<ConversationDefinition> definitions)
    {
        Definitions = definitions;
        Index = definitions.ToFrozenDictionary(d => d.Id, StringComparer.Ordinal);
    }

    public static ConversationCatalog Empty { get; } = new(ImmutableArray<ConversationDefinition>.Empty);

    public ImmutableArray<ConversationDefinition> Definitions { get; }

    public FrozenDictionary<string, ConversationDefinition> Index { get; }

    public bool TryGet(string? id, out ConversationDefinition definition)
    {
        if (id is not null && Index.TryGetValue(id, out var found))
        {
            definition = found;
            return true;
        }

        definition = null!;
        return false;
    }

    /// <summary>
    /// Checks unique conversation ids; participants and speakers exist; at least one step; unique step ids and unique choice ids per
    /// conversation; effects name real characters; start entries and destinations name real steps or conversations.
    /// Conditions (when, requires) are checked too: see Conditions.Check. Encounters, when given, are the fights a lineupReady condition may name; null skips that check.
    /// A default array means no conversations. Paths read "conversations/2/steps/0/choices/1/next/0/step".
    /// </summary>
    public static Result<ConversationCatalog> Create(ImmutableArray<ConversationDefinition> definitions, CharacterCatalog characters, EncounterCatalog? encounters = null)
    {
        if (definitions.IsDefault)
        {
            return Result<ConversationCatalog>.Ok(Empty);
        }

        var details = ImmutableArray.CreateBuilder<RuleErrorDetail>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var choicesByConversation = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var d in definitions)
        {
            if (d is not null && !string.IsNullOrEmpty(d.Id))
            {
                ids.Add(d.Id);
                var choiceIds = new HashSet<string>(StringComparer.Ordinal);
                for (var s = 0; !d.Steps.IsDefault && s < d.Steps.Length; s++)
                {
                    var choices = d.Steps[s]?.Choices ?? default;
                    for (var c = 0; !choices.IsDefault && c < choices.Length; c++)
                    {
                        if (!string.IsNullOrEmpty(choices[c].Id))
                        {
                            choiceIds.Add(choices[c].Id);
                        }
                    }
                }

                choicesByConversation[d.Id] = choiceIds;
            }
        }

        var refs = new ConditionReferences(characters, encounters, choicesByConversation);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < definitions.Length; i++)
        {
            var d = definitions[i];
            var path = "conversations/" + Num(i);
            if (d is null)
            {
                details.Add(new RuleErrorDetail(path, "conversation must not be null"));
                continue;
            }

            if (string.IsNullOrEmpty(d.Id))
            {
                details.Add(new RuleErrorDetail(path + "/id", "id must not be empty"));
            }
            else
            {
                if (d.Id.Length > CharacterCatalog.MaxIdLength)
                {
                    details.Add(new RuleErrorDetail(path + "/id", $"id must be 1 to {CharacterCatalog.MaxIdLength} characters"));
                }

                if (!seen.Add(d.Id))
                {
                    details.Add(new RuleErrorDetail(path + "/id", $"duplicate conversation id: {d.Id}"));
                }
            }

            CheckConversation(d, path, characters, ids, refs, details);
        }

        if (details.Count > 0)
        {
            return Result<ConversationCatalog>.Fail(
                new RuleError(ErrorCode.VALIDATION_ERROR, "Conversation catalog is invalid: " + details[0].Message, details.ToImmutable()));
        }

        return Result<ConversationCatalog>.Ok(new ConversationCatalog(definitions));
    }

    private static void CheckConversation(
        ConversationDefinition d,
        string path,
        CharacterCatalog characters,
        HashSet<string> conversationIds,
        ConditionReferences refs,
        ImmutableArray<RuleErrorDetail>.Builder details)
    {
        for (var p = 0; !d.Participants.IsDefault && p < d.Participants.Length; p++)
        {
            if (!characters.TryGet(d.Participants[p], out _))
            {
                details.Add(new RuleErrorDetail(path + "/participants/" + Num(p), $"unknown character: {d.Participants[p]}"));
            }
        }

        if (d.Steps.IsDefaultOrEmpty)
        {
            details.Add(new RuleErrorDetail(path + "/steps", "a conversation needs at least one step"));
            return;
        }

        var stepIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var step in d.Steps)
        {
            if (step is not null && !string.IsNullOrEmpty(step.Id))
            {
                stepIds.Add(step.Id);
            }
        }

        for (var s = 0; !d.Start.IsDefault && s < d.Start.Length; s++)
        {
            if (!stepIds.Contains(d.Start[s].StepId))
            {
                details.Add(new RuleErrorDetail(path + "/start/" + Num(s) + "/step", $"unknown step: {d.Start[s].StepId}"));
            }

            Conditions.Conditions.Check(d.Start[s].When, path + "/start/" + Num(s) + "/when", refs, details);
        }

        var seenSteps = new HashSet<string>(StringComparer.Ordinal);
        var seenChoices = new HashSet<string>(StringComparer.Ordinal);
        for (var s = 0; s < d.Steps.Length; s++)
        {
            var step = d.Steps[s];
            var stepPath = path + "/steps/" + Num(s);
            if (step is null)
            {
                details.Add(new RuleErrorDetail(stepPath, "step must not be null"));
                continue;
            }

            if (string.IsNullOrEmpty(step.Id))
            {
                details.Add(new RuleErrorDetail(stepPath + "/id", "id must not be empty"));
            }
            else if (step.Id.Length > CharacterCatalog.MaxIdLength)
            {
                details.Add(new RuleErrorDetail(stepPath + "/id", $"id must be 1 to {CharacterCatalog.MaxIdLength} characters"));
            }
            else if (!seenSteps.Add(step.Id))
            {
                details.Add(new RuleErrorDetail(stepPath + "/id", $"duplicate step id in conversation {d.Id}: {step.Id}"));
            }

            for (var l = 0; !step.Lines.IsDefault && l < step.Lines.Length; l++)
            {
                if (step.Lines[l].Speaker is Speaker.Character c && !characters.TryGet(c.Id, out _))
                {
                    details.Add(new RuleErrorDetail(stepPath + "/lines/" + Num(l) + "/speaker", $"unknown speaker: {c.Id}"));
                }

                Conditions.Conditions.Check(step.Lines[l].When, stepPath + "/lines/" + Num(l) + "/when", refs, details);
            }

            CheckDestinations(step.Next, stepPath + "/next", stepIds, conversationIds, refs, details);

            for (var c = 0; !step.Choices.IsDefault && c < step.Choices.Length; c++)
            {
                var choice = step.Choices[c];
                var choicePath = stepPath + "/choices/" + Num(c);
                if (string.IsNullOrEmpty(choice.Id))
                {
                    details.Add(new RuleErrorDetail(choicePath + "/id", "id must not be empty"));
                }
                else if (choice.Id.Length > CharacterCatalog.MaxIdLength)
                {
                    details.Add(new RuleErrorDetail(choicePath + "/id", $"id must be 1 to {CharacterCatalog.MaxIdLength} characters"));
                }
                else if (!seenChoices.Add(choice.Id))
                {
                    details.Add(new RuleErrorDetail(choicePath + "/id", $"duplicate choice id in conversation {d.Id}: {choice.Id}"));
                }

                for (var e = 0; !choice.Effects.IsDefault && e < choice.Effects.Length; e++)
                {
                    var effectCharacter = choice.Effects[e] switch
                    {
                        Effect.Relationship r => r.CharacterId,
                        Effect.Join j => j.CharacterId,
                        Effect.Leave l => l.CharacterId,
                        _ => null,
                    };
                    var effectFlag = choice.Effects[e] switch
                    {
                        Effect.SetFlag f => f.FlagId,
                        Effect.ClearFlag f => f.FlagId,
                        _ => null,
                    };
                    if (choice.Effects[e] is Effect.HandOff handOff)
                    {
                        var handOffPath = choicePath + "/effects/" + Num(e);
                        if (refs.Encounters is not null && !refs.Encounters.TryGet(handOff.FightId, out _))
                        {
                            details.Add(new RuleErrorDetail(handOffPath + "/fight", $"unknown fight: {handOff.FightId}"));
                        }

                        if (e != choice.Effects.Length - 1)
                        {
                            details.Add(new RuleErrorDetail(handOffPath, "a hand-off must be the last effect of its choice"));
                        }

                        if (!choice.Next.IsDefaultOrEmpty)
                        {
                            details.Add(new RuleErrorDetail(choicePath + "/next", "a choice that hands off to a fight cannot also lead on"));
                        }
                    }

                    if (effectFlag is not null && effectFlag.Length > CharacterCatalog.MaxIdLength)
                    {
                        details.Add(new RuleErrorDetail(choicePath + "/effects/" + Num(e) + "/flag", $"flag must be 1 to {CharacterCatalog.MaxIdLength} characters"));
                    }

                    if (effectFlag is not null && effectFlag.Length == 0)
                    {
                        details.Add(new RuleErrorDetail(choicePath + "/effects/" + Num(e) + "/flag", "flag must not be empty"));
                    }

                    if (effectCharacter is not null && !characters.TryGet(effectCharacter, out _))
                    {
                        details.Add(new RuleErrorDetail(choicePath + "/effects/" + Num(e) + "/character", $"unknown character: {effectCharacter}"));
                    }
                }

                CheckReactions(choice, choicePath + "/reactions", d, details);
                Conditions.Conditions.Check(choice.When, choicePath + "/when", refs, details);
                Conditions.Conditions.Check(choice.Requires, choicePath + "/requires", refs, details);
                CheckDestinations(choice.Next, choicePath + "/next", stepIds, conversationIds, refs, details);
            }
        }
    }

    private static void CheckReactions(ChoiceDefinition choice, string path, ConversationDefinition conversation, ImmutableArray<RuleErrorDetail>.Builder details)
    {
        if (choice.Reactions.IsDefault)
        {
            return;
        }

        var named = new HashSet<string>(StringComparer.Ordinal);
        for (var r = 0; r < choice.Reactions.Length; r++)
        {
            var over = choice.Reactions[r];
            var overPath = path + "/" + Num(r);
            if (over is null)
            {
                details.Add(new RuleErrorDetail(overPath, "reaction must not be null"));
                continue;
            }

            if (conversation.Participants.IsDefault || !conversation.Participants.Contains(over.CharacterId, StringComparer.Ordinal))
            {
                details.Add(new RuleErrorDetail(overPath + "/character", $"reaction must name a participant of the conversation: {over.CharacterId}"));
            }
            else if (!named.Add(over.CharacterId))
            {
                details.Add(new RuleErrorDetail(overPath + "/character", $"more than one reaction for character: {over.CharacterId}"));
            }

            if (over.Silence)
            {
                if (over.Text is not null || over.Affection is not null || over.Trust is not null)
                {
                    details.Add(new RuleErrorDetail(overPath + "/silence", "a silenced reaction cannot also have text or values"));
                }
            }
            else if (string.IsNullOrEmpty(over.Text))
            {
                details.Add(new RuleErrorDetail(overPath + "/text", "a reaction needs silence or text"));
            }

            if ((over.Affection is { } a && !double.IsFinite(a)) || (over.Trust is { } t && !double.IsFinite(t)))
            {
                details.Add(new RuleErrorDetail(overPath, "affection and trust must be finite numbers"));
            }
        }
    }

    private static void CheckDestinations(
        ImmutableArray<Destination> next,
        string path,
        HashSet<string> stepIds,
        HashSet<string> conversationIds,
        ConditionReferences refs,
        ImmutableArray<RuleErrorDetail>.Builder details)
    {
        for (var n = 0; !next.IsDefault && n < next.Length; n++)
        {
            switch (next[n])
            {
                case Destination.ToStep to when !stepIds.Contains(to.StepId):
                    details.Add(new RuleErrorDetail(path + "/" + Num(n) + "/step", $"unknown step: {to.StepId}"));
                    break;
                case Destination.ToConversation to when !conversationIds.Contains(to.ConversationId):
                    details.Add(new RuleErrorDetail(path + "/" + Num(n) + "/conversation", $"unknown conversation: {to.ConversationId}"));
                    break;
            }

            Conditions.Conditions.Check(next[n].When, path + "/" + Num(n) + "/when", refs, details);
        }
    }

    private static string Num(int i) => i.ToString(CultureInfo.InvariantCulture);
}
