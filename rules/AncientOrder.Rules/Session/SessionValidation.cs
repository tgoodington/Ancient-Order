using System.Collections.Immutable;
using System.Globalization;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Personality;

namespace AncientOrder.Rules.Session;

/// <summary>Input checks with the TypeScript messages. Each returns null when the input is fine.</summary>
public static class SessionValidation
{
    public const int MaxIdLength = 64;

    public static RuleError? IdString(string name, string? input)
    {
        if (input is null)
        {
            return Invalid($"{name} must be string");
        }

        return input.Length > MaxIdLength ? Invalid($"{name} must NOT have more than {MaxIdLength} characters") : null;
    }

    /// <summary>Order: object, at least one delta, unknown trait, then finite values in trait order (the TypeScript walked the object's keys in insertion order).</summary>
    public static RuleError? PersonalityAdjustment(PersonalityAdjustment? adjustment)
    {
        if (adjustment?.Deltas is null)
        {
            return Invalid("adjustment must be object");
        }

        if (adjustment.Deltas.Count < 1)
        {
            return Invalid("adjustment must NOT have fewer than 1 properties");
        }

        foreach (var key in adjustment.Deltas.Keys)
        {
            if (!PersonalityRules.Traits.Contains(key))
            {
                return Invalid("adjustment must NOT have additional properties");
            }
        }

        foreach (var trait in PersonalityRules.Traits)
        {
            if (adjustment.Deltas.TryGetValue(trait, out var value) && !double.IsFinite(value))
            {
                return Invalid($"{TraitName(trait)} must be number");
            }
        }

        return null;
    }

    public static RuleError? LineupIds(ImmutableArray<string?> ids, int maxSize)
    {
        if (ids.IsDefault)
        {
            return Invalid("npcIds must be array");
        }

        if (ids.Length > maxSize)
        {
            return Invalid($"npcIds must NOT have more than {maxSize} items");
        }

        for (var i = 0; i < ids.Length; i++)
        {
            var problem = IdString($"npcIds/{i}", ids[i]);
            if (problem is not null)
            {
                return problem;
            }
        }

        return null;
    }

    public const int MaxOrders = 10;

    /// <summary>Order list shape: a list, at most 10, each an order with a combatant id of at most 64 characters, a target id that is null or at most 64 characters, and finite energy segments.</summary>
    public static RuleError? CombatOrders(IReadOnlyList<CombatAction?>? orders)
    {
        if (orders is null)
        {
            return Invalid("actions must be array");
        }

        if (orders.Count > MaxOrders)
        {
            return Invalid($"actions must NOT have more than {MaxOrders} items");
        }

        for (var i = 0; i < orders.Count; i++)
        {
            var at = $"actions/{i.ToString(CultureInfo.InvariantCulture)}";
            var order = orders[i];
            if (order is null)
            {
                return Invalid($"{at} must be object");
            }

            var combatant = IdString($"{at}/combatantId", order.CombatantId);
            if (combatant is not null)
            {
                return combatant;
            }

            if (order.TargetId is not null)
            {
                var target = IdString($"{at}/targetId", order.TargetId);
                if (target is not null)
                {
                    return target;
                }
            }

            if (order.EnergySegments is { } energy && !double.IsFinite(energy))
            {
                return Invalid($"{at}/energySegments must be number");
            }
        }

        return null;
    }

    private static RuleError Invalid(string message) => new(ErrorCode.VALIDATION_ERROR, message);

    private static string TraitName(Trait trait) => trait switch
    {
        Trait.Patience => "patience",
        Trait.Empathy => "empathy",
        Trait.Cunning => "cunning",
        Trait.Logic => "logic",
        Trait.Kindness => "kindness",
        Trait.Charisma => "charisma",
        _ => throw new ArgumentOutOfRangeException(nameof(trait)),
    };
}
