using System.Collections.Immutable;
using System.Globalization;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.HandOff;
using AncientOrder.Rules.Party;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Conditions;

/// <summary>What the content check needs to confirm a condition's references exist. Encounters null skips the fight check.</summary>
internal sealed record ConditionReferences(
    CharacterCatalog Characters,
    EncounterCatalog? Encounters,
    IReadOnlyDictionary<string, HashSet<string>> ChoicesByConversation);

/// <summary>
/// The one condition evaluator. Explain builds the tree and Holds reads its root, so they cannot disagree.
/// A list means all conditions must hold; an empty or default list holds.
/// </summary>
public static class Conditions
{
    public static bool Holds(GameContent content, GameState game, ImmutableArray<Condition> conditions) =>
        Explain(content, game, conditions).Met;

    /// <summary>An All node over the list, one child per condition, every node saying whether it is met.</summary>
    public static RequirementView Explain(GameContent content, GameState game, ImmutableArray<Condition> conditions)
    {
        var children = Evaluate(content, game, conditions);
        return new RequirementView.All(children, children.All(c => c.Met));
    }

    private static ImmutableArray<RequirementView> Evaluate(GameContent content, GameState game, ImmutableArray<Condition> conditions) =>
        conditions.IsDefaultOrEmpty ? [] : [.. conditions.Select(c => Evaluate(content, game, c))];

    private static RequirementView Evaluate(GameContent content, GameState game, Condition condition)
    {
        switch (condition)
        {
            case Condition.Flag f:
                return new RequirementView.Flag(f.FlagId, game.Memory.HasFlag(f.FlagId));
            case Condition.Chose c:
                return new RequirementView.Chose(c.ConversationId, c.ChoiceId, game.Memory.HasChosen(c.ConversationId, c.ChoiceId));
            case Condition.Seen s:
                return new RequirementView.Seen(s.ConversationId, game.Memory.HasSeen(s.ConversationId));
            case Condition.TraitRange t:
                var value = game.Player.Personality[t.Trait];
                return new RequirementView.TraitRange(t.Trait, t.AtLeast, t.AtMost, value, InRange(value, t.AtLeast, t.AtMost));
            case Condition.RelationshipRange r:
                var known = game.Characters.TryGetValue(r.CharacterId, out var state);
                var current = !known ? 0 : r.Measure == RelationshipMeasure.Affection ? state!.Affection : state!.Trust;
                var name = content.Characters.TryGet(r.CharacterId, out var definition) ? definition.Name : r.CharacterId;
                return new RequirementView.RelationshipRange(
                    r.CharacterId, name, r.Measure, r.AtLeast, r.AtMost, current, known && InRange(current, r.AtLeast, r.AtMost));
            case Condition.InParty p:
                var memberName = content.Characters.TryGet(p.CharacterId, out var member) ? member.Name : p.CharacterId;
                return new RequirementView.InParty(p.CharacterId, memberName, game.Party.Contains(p.CharacterId, StringComparer.Ordinal));
            case Condition.LineupReady l:
                var readiness = LineupRules.Ready(content, game, HandOffRules.LimitsFor(content, l.FightId));
                return new RequirementView.LineupReady(l.FightId, readiness.Reason, readiness.Ready);
            case Condition.All a:
                var all = Evaluate(content, game, a.Conditions);
                return new RequirementView.All(all, all.All(c => c.Met));
            case Condition.Any a:
                var any = Evaluate(content, game, a.Conditions);
                return new RequirementView.Any(any, any.Any(c => c.Met));
            case Condition.Negation n:
                var inner = Evaluate(content, game, n.Inner);
                return new RequirementView.Negation(inner, !inner.Met);
            default:
                throw new InvalidOperationException($"Unknown condition {condition?.GetType().Name}");
        }
    }

    private static bool InRange(double value, double? atLeast, double? atMost) =>
        (atLeast is null || value >= atLeast) && (atMost is null || value <= atMost);

    /// <summary>
    /// Adds a detail for every problem in the list: null conditions, a trait or relationship condition with neither threshold, a
    /// missing id, and references to characters, conversations, choices, or fights that do not exist. Paths read "path/0/conditions/1".
    /// </summary>
    internal static void Check(ImmutableArray<Condition> conditions, string path, ConditionReferences refs, ImmutableArray<RuleErrorDetail>.Builder details)
    {
        for (var i = 0; !conditions.IsDefault && i < conditions.Length; i++)
        {
            Check(conditions[i], path + "/" + Num(i), refs, details);
        }
    }

    private static void Check(Condition? condition, string path, ConditionReferences refs, ImmutableArray<RuleErrorDetail>.Builder details)
    {
        switch (condition)
        {
            case null:
                details.Add(new RuleErrorDetail(path, "condition must not be null"));
                break;
            case Condition.Flag f:
                if (f.FlagId is { Length: > State.CharacterCatalog.MaxIdLength })
                {
                    details.Add(new RuleErrorDetail(path + "/flag", $"flag must be 1 to {State.CharacterCatalog.MaxIdLength} characters"));
                }

                if (string.IsNullOrEmpty(f.FlagId))
                {
                    details.Add(new RuleErrorDetail(path + "/flag", "flag must not be empty"));
                }

                break;
            case Condition.Chose c:
                if (!refs.ChoicesByConversation.TryGetValue(c.ConversationId ?? string.Empty, out var choices))
                {
                    details.Add(new RuleErrorDetail(path + "/conversation", $"unknown conversation: {c.ConversationId}"));
                }
                else if (!choices.Contains(c.ChoiceId ?? string.Empty))
                {
                    details.Add(new RuleErrorDetail(path + "/choice", $"unknown choice in conversation {c.ConversationId}: {c.ChoiceId}"));
                }

                break;
            case Condition.Seen s:
                if (!refs.ChoicesByConversation.ContainsKey(s.ConversationId ?? string.Empty))
                {
                    details.Add(new RuleErrorDetail(path + "/conversation", $"unknown conversation: {s.ConversationId}"));
                }

                break;
            case Condition.TraitRange t:
                if (t.AtLeast is null && t.AtMost is null)
                {
                    details.Add(new RuleErrorDetail(path, "a trait condition needs atLeast or atMost"));
                }

                break;
            case Condition.RelationshipRange r:
                if (!refs.Characters.TryGet(r.CharacterId, out _))
                {
                    details.Add(new RuleErrorDetail(path + "/character", $"unknown character: {r.CharacterId}"));
                }

                if (r.AtLeast is null && r.AtMost is null)
                {
                    details.Add(new RuleErrorDetail(path, "a relationship condition needs atLeast or atMost"));
                }

                break;
            case Condition.InParty p:
                if (!refs.Characters.TryGet(p.CharacterId, out _))
                {
                    details.Add(new RuleErrorDetail(path + "/character", $"unknown character: {p.CharacterId}"));
                }

                break;
            case Condition.LineupReady l:
                if (l.FightId is not null && refs.Encounters is not null && !refs.Encounters.TryGet(l.FightId, out _))
                {
                    details.Add(new RuleErrorDetail(path + "/fight", $"unknown fight: {l.FightId}"));
                }

                break;
            case Condition.All a:
                Check(a.Conditions, path + "/conditions", refs, details);
                break;
            case Condition.Any a:
                Check(a.Conditions, path + "/conditions", refs, details);
                break;
            case Condition.Negation n:
                if (n.Inner is null)
                {
                    details.Add(new RuleErrorDetail(path + "/condition", "a not condition needs a condition"));
                }
                else
                {
                    Check(n.Inner, path + "/condition", refs, details);
                }

                break;
        }
    }

    private static string Num(int i) => i.ToString(CultureInfo.InvariantCulture);
}
