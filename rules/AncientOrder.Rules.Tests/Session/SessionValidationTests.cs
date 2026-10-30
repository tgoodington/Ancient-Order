using System.Collections.Immutable;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Personality;
using AncientOrder.Rules.Session;
using AncientOrder.Rules.Tests.Support;

namespace AncientOrder.Rules.Tests.Session;

// Carried over from src/session/validation.test.ts. The typed API makes some checks impossible to fail (a string where a
// number belongs, extra keys, a missing action type); those cases are dropped and listed in the port record.
// Plus the 64-character id rule and null strings, which stand in for "not a string".
public class ValidatePersonalityAdjustment
{
    private static PersonalityAdjustment Deltas(params (Trait Trait, double Delta)[] deltas) => Adj.Of(deltas);

    [Fact(DisplayName = "accepts one or more known traits with finite numbers")]
    public void Accepts_known_traits()
    {
        Assert.Null(SessionValidation.PersonalityAdjustment(Deltas((Trait.Patience, 5))));
        Assert.Null(SessionValidation.PersonalityAdjustment(Deltas((Trait.Patience, -5), (Trait.Charisma, 0.5))));
    }

    // Changed: only the null cases remain; a string, number, or array cannot be passed as an adjustment.
    [Fact(DisplayName = "rejects non-object %j")]
    public void Rejects_non_object()
    {
        Assert.Equal(ErrorCode.VALIDATION_ERROR, SessionValidation.PersonalityAdjustment(null)!.Code);
        Assert.Equal(ErrorCode.VALIDATION_ERROR, SessionValidation.PersonalityAdjustment(new PersonalityAdjustment(null!))!.Code);
    }

    [Fact(DisplayName = "rejects an empty object")]
    public void Rejects_an_empty_object()
    {
        var error = SessionValidation.PersonalityAdjustment(new PersonalityAdjustment(ImmutableDictionary<Trait, double>.Empty));
        Assert.Contains("fewer than 1", error!.Message, StringComparison.Ordinal);
    }

    // Changed: an unknown trait is a Trait value outside the enum; prototype names cannot be keys of a typed dictionary.
    [Fact(DisplayName = "rejects unknown traits, including prototype names")]
    public void Rejects_unknown_traits()
    {
        Assert.Equal(ErrorCode.VALIDATION_ERROR, SessionValidation.PersonalityAdjustment(Deltas(((Trait)99, 1)))!.Code);
        Assert.Equal(ErrorCode.VALIDATION_ERROR, SessionValidation.PersonalityAdjustment(Deltas((Trait.Patience, 1), ((Trait)6, 1)))!.Code);
    }

    // Changed: only the non-finite numbers remain; a string, null, or object value cannot be a double.
    [Fact(DisplayName = "rejects non-finite-number value %j")]
    public void Rejects_non_finite_values()
    {
        foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var error = SessionValidation.PersonalityAdjustment(Deltas((Trait.Patience, value)));
            Assert.Equal(ErrorCode.VALIDATION_ERROR, error!.Code);
            Assert.Equal("patience must be number", error.Message);
        }
    }
}

public class ValidateIdString
{
    // Changed: the limit is fixed at 64 characters (no limit argument).
    [Fact(DisplayName = "accepts a string with no limit, or within the limit")]
    public void Accepts_within_the_limit()
    {
        Assert.Null(SessionValidation.IdString("id", "x"));
        Assert.Null(SessionValidation.IdString("id", string.Empty));
        Assert.Null(SessionValidation.IdString("id", new string('x', 64)));
    }

    // Changed: null stands in for "not a string"; the limit is 64 and the 65-character message names the field.
    [Fact(DisplayName = "rejects non-strings and strings over the limit")]
    public void Rejects_null_and_over_the_limit()
    {
        var missing = SessionValidation.IdString("id", null);
        Assert.Equal(ErrorCode.VALIDATION_ERROR, missing!.Code);
        Assert.Equal("id must be string", missing.Message);
        Assert.Equal(
            "startingSceneId must NOT have more than 64 characters",
            SessionValidation.IdString("startingSceneId", new string('x', 65))!.Message);
        Assert.Equal(ErrorCode.VALIDATION_ERROR, SessionValidation.IdString("id", new string('x', 500))!.Code);
    }

    [Fact(DisplayName = "the limit is the published constant of 64")]
    public void The_limit_is_64()
    {
        Assert.Equal(64, SessionValidation.MaxIdLength);
    }
}

public class ValidateCombatActions
{
    private static readonly CombatAction Ok = new("a", ActionType.Attack, null, null);

    private static RuleError? Check(params CombatAction?[] orders) => SessionValidation.CombatOrders(orders);

    [Fact(DisplayName = "accepts an empty array and well-formed actions")]
    public void Accepts_well_formed()
    {
        Assert.Null(Check());
        Assert.Null(Check(Ok));
        Assert.Null(Check(Ok with { TargetId = null }, Ok with { TargetId = "b" }));
        Assert.Null(Check(Ok with { EnergySegments = 2 }));
    }

    // Changed: a null list is the only non-array.
    [Fact(DisplayName = "rejects a non-array")]
    public void Rejects_a_non_array()
    {
        var error = SessionValidation.CombatOrders(null);
        Assert.Equal(ErrorCode.VALIDATION_ERROR, error!.Code);
        Assert.Equal("actions must be array", error.Message);
    }

    [Fact(DisplayName = "allows 10 actions and rejects 11")]
    public void Allows_ten_rejects_eleven()
    {
        Assert.Null(Check([.. Enumerable.Repeat<CombatAction?>(Ok, 10)]));
        var error = Check([.. Enumerable.Repeat<CombatAction?>(Ok, 11)]);
        Assert.Contains("more than 10", error!.Message, StringComparison.Ordinal);
    }

    // Changed: a null element is the only non-object.
    [Fact(DisplayName = "rejects non-object items")]
    public void Rejects_non_object_items()
    {
        var error = Check((CombatAction?)null);
        Assert.Equal(ErrorCode.VALIDATION_ERROR, error!.Code);
        Assert.Equal("actions/0 must be object", error.Message);
    }

    // Changed: the action type is an enum and cannot be missing; a missing combatant id is a null string.
    [Fact(DisplayName = "requires combatantId and type")]
    public void Requires_combatant_id()
    {
        var error = Check(Ok with { CombatantId = null! });
        Assert.Equal("actions/0/combatantId must be string", error!.Message);
    }

    // Changed: extra keys and wrongly typed type/energy values cannot be written in the typed API; the ids and the
    // non-finite energy remain, plus the 64-character limit on both ids.
    [Fact(DisplayName = "rejects extra keys and wrongly typed fields")]
    public void Rejects_bad_fields()
    {
        Assert.Equal("actions/0/combatantId must NOT have more than 64 characters", Check(Ok with { CombatantId = new string('x', 65) })!.Message);
        Assert.Equal("actions/0/targetId must NOT have more than 64 characters", Check(Ok with { TargetId = new string('x', 65) })!.Message);
        Assert.Null(Check(Ok with { CombatantId = new string('x', 64), TargetId = new string('y', 64) }));
        Assert.Equal(ErrorCode.VALIDATION_ERROR, Check(Ok with { EnergySegments = double.NaN })!.Code);
        Assert.Equal("actions/0/energySegments must be number", Check(Ok with { EnergySegments = double.PositiveInfinity })!.Message);
    }

    [Fact(DisplayName = "reports the index of the offending item")]
    public void Reports_the_index()
    {
        Assert.Contains("actions/1", Check(Ok, Ok with { CombatantId = null! })!.Message, StringComparison.Ordinal);
    }
}

public class ValidateTeamIds
{
    private static RuleError? Check(params string?[] ids) => SessionValidation.LineupIds([.. ids], 2);

    [Fact(DisplayName = "accepts exactly two strings of at most 64 characters")]
    public void Accepts_two_strings()
    {
        Assert.Null(Check("a", "b"));
        Assert.Null(Check("a", new string('x', 64)));
    }

    // Changed: a default (unset) array is the only non-array.
    [Fact(DisplayName = "rejects a non-array")]
    public void Rejects_a_non_array()
    {
        var error = SessionValidation.LineupIds(default, 2);
        Assert.Equal(ErrorCode.VALIDATION_ERROR, error!.Code);
        Assert.Equal("npcIds must be array", error.Message);
    }

    // Changed: only more than the content's max is refused here; a short lineup is fine while the player is still picking.
    [Fact(DisplayName = "rejects any count other than 2")]
    public void Rejects_more_than_max()
    {
        Assert.Equal("npcIds must NOT have more than 2 items", Check("a", "b", "c")!.Message);
        Assert.Null(Check());
        Assert.Null(Check("a"));
    }

    // Changed: a null entry stands in for a non-string.
    [Fact(DisplayName = "rejects non-string and over-long entries")]
    public void Rejects_null_and_over_long_entries()
    {
        Assert.Equal("npcIds/1 must be string", Check("a", null)!.Message);
        Assert.Contains("more than 64", Check(new string('x', 65), "b")!.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "the limit follows the lineup max")]
    public void Count_follows_the_lineup_max()
    {
        Assert.Null(SessionValidation.LineupIds(["a", "b", "c"], 3));
        Assert.Equal("npcIds must NOT have more than 3 items", SessionValidation.LineupIds(["a", "b", "c", "d"], 3)!.Message);
    }
}
