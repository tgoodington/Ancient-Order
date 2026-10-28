using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Saves;
using AncientOrder.Rules.State;
using AncientOrder.Rules.Tests.Fixtures;
using AncientOrder.Rules.Tests.Support;
using static AncientOrder.Rules.Tests.Support.SaveFixtures;

namespace AncientOrder.Rules.Tests.Saves;

// Carried over from src/persistence/validation.test.ts. validateGameState becomes SaveFormat.Read over a save document built from a real game
// and edited the way the TypeScript test edited its fixture. Changed: cases whose check moved (a wrong lineup size is now cleared on load, not
// refused; an old-format game with no team is now refused). Dropped: the narrative phases (narrativeState persistence, narrative deep validation).
// New: round trip, identical bytes across cultures, size in UTF-8 bytes, strict reading, and the upgrade pipeline.
internal static class FormatEdit
{
    public static void SetPersonality(JsonObject game, double patience, double empathy, double cunning, double logic, double kindness, double charisma)
    {
        var p = (JsonObject)game["player"]!["personality"]!;
        p["patience"] = patience;
        p["empathy"] = empathy;
        p["cunning"] = cunning;
        p["logic"] = logic;
        p["kindness"] = kindness;
        p["charisma"] = charisma;
    }

    public static Result<LoadedSave> FightEdited(Action<JsonObject> editCombat) =>
        ReadEdited(FightGame(), g => editCombat((JsonObject)g["combat"]!));
}

public class ValidateGameState
{
    [Fact(DisplayName = "accepts a valid GameState")]
    public void Accepts_a_valid_game()
    {
        var game = MakeGame();
        var loaded = ReadOk(WriteOk(game));
        StateAssert.Equivalent(game, loaded.Game);
        Assert.True(loaded.Report.IsEmpty);
    }

    [Fact(DisplayName = "rejects null")]
    public void Rejects_null()
    {
        Refused(ReadEdited(MakeGame(), g => g.Parent!["game"] = null));
        Refused(Read("null"));
    }

    [Fact(DisplayName = "rejects a primitive")]
    public void Rejects_a_primitive()
    {
        Refused(Read("42"));
        Refused(Read("\"string\""));
        Refused(ReadEnvelopeEdited(MakeGame(), e => e["game"] = 42));
        Refused(ReadEnvelopeEdited(MakeGame(), e => e["game"] = "string"));
    }

    [Fact(DisplayName = "rejects missing player field")]
    public void Rejects_missing_player()
    {
        Refused(ReadEdited(MakeGame(), g => g.Remove("player")));
    }

    [Fact(DisplayName = "rejects missing personality trait")]
    public void Rejects_missing_trait()
    {
        Refused(ReadEdited(MakeGame(), g =>
        {
            var p = (JsonObject)g["player"]!["personality"]!;
            p.Remove("kindness");
            p.Remove("charisma");
        }));
    }

    [Fact(DisplayName = "rejects missing timestamp")]
    public void Rejects_missing_timestamp()
    {
        Refused(ReadEdited(MakeGame(), g => g.Remove("timestamp")));
    }

    [Fact(DisplayName = "accepts combatState: null")]
    public void Accepts_no_combat()
    {
        var loaded = ReadEdited(MakeGame(), g => g["combat"] = null);
        Assert.Null(loaded.Value.Game.Combat);
    }

    [Fact(DisplayName = "accepts a valid combatState shape")]
    public void Accepts_a_valid_combat_shape()
    {
        var game = FightGame();
        var loaded = ReadOk(WriteOk(game, LongFightContent), LongFightContent);
        StateAssert.Equivalent(game, loaded.Game);
        Assert.NotNull(loaded.Game.Combat);
    }

    [Fact(DisplayName = "accepts npcs as empty Record")]
    public void Accepts_no_characters()
    {
        var loaded = ReadEdited(MakeGame(), g => g["characters"] = new JsonArray()).Value;
        Assert.Equal(3, loaded.Game.Characters.Count);
        Assert.Equal(3, loaded.Report.Adjustments.Count(a => a.Kind == LoadAdjustmentKind.CharacterAdded));
    }

    [Fact(DisplayName = "rejects npcs as an array")]
    public void Rejects_characters_as_an_object()
    {
        Refused(ReadEdited(MakeGame(), g => g["characters"] = new JsonObject()));
    }

    [Fact(DisplayName = "rejects an NPC missing the affection field")]
    public void Rejects_character_without_affection()
    {
        Refused(ReadEdited(MakeGame(), g => ((JsonObject)g["characters"]![0]!).Remove("affection")));
    }
}

public class NarrativeStatePersistence
{
    [Fact(DisplayName = "validateGameState rejects reactionProgress with a non-number rate")]
    public void Rejects_a_non_number_rate()
    {
        var bad = ReadEdited(MakeGame(), g => g["reactionProgress"] = new JsonArray(
            new JsonObject { ["id"] = "player_1", ["block"] = "lots", ["dodge"] = 0, ["parry"] = 0 }));
        Refused(bad);
    }
}

public class ValidateGameStatePersonalityDepthPhase2
{
    private static Result<LoadedSave> WithPersonality(double a, double b, double c, double d, double e, double f) =>
        ReadEdited(MakeGame(), g => FormatEdit.SetPersonality(g, a, b, c, d, e, f));

    [Fact(DisplayName = "rejects personality trait below minimum 5")]
    public void Below_minimum()
    {
        var error = Refused(WithPersonality(3, 30, 17, 17, 17, 16));
        Assert.Contains("below minimum", error.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "rejects personality trait above maximum 35")]
    public void Above_maximum()
    {
        var error = Refused(WithPersonality(40, 15, 15, 10, 10, 10));
        Assert.Contains("above maximum", error.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "rejects personality sum not equal to 100")]
    public void Sum_not_100()
    {
        var error = Refused(WithPersonality(15, 15, 15, 15, 15, 15));
        Assert.Contains("does not equal 100", error.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "returns specific error messages for personality failures")]
    public void Specific_messages()
    {
        var error = Refused(WithPersonality(3, 30, 17, 17, 17, 16));
        Assert.Contains(error.Details, d => d.Message.Contains("patience", StringComparison.OrdinalIgnoreCase));
        Assert.StartsWith("player.personality: ", error.Details[0].Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "accepts personality at boundary values (5 and 35)")]
    public void Boundary_values()
    {
        Assert.True(WithPersonality(5, 35, 15, 15, 15, 15).IsOk);
    }
}

public class ValidateGameStateCombatStateDepthPhase5
{
    [Fact(DisplayName = "rejects combatState with invalid phase value")]
    public void Invalid_phase() => Refused(FormatEdit.FightEdited(c => c["phase"] = "INVALID_PHASE"));

    [Fact(DisplayName = "rejects combatState with invalid status value")]
    public void Invalid_status() => Refused(FormatEdit.FightEdited(c => c["status"] = "unknown"));

    [Fact(DisplayName = "rejects combatState with missing playerParty array")]
    public void Missing_player_party() => Refused(FormatEdit.FightEdited(c => c.Remove("playerParty")));

    [Fact(DisplayName = "rejects combatState with non-number round")]
    public void Non_number_round() => Refused(FormatEdit.FightEdited(c => c["round"] = "one"));

    [Fact(DisplayName = "accepts combatState with all 5 valid CombatPhase values")]
    public void All_five_phases()
    {
        foreach (var phase in new[] { "aiDecision", "visualInfo", "pcDeclaration", "actionResolution", "perAttack" })
        {
            var loaded = FormatEdit.FightEdited(c => c["phase"] = phase);
            Assert.True(loaded.IsOk, phase + ": " + (loaded.IsOk ? string.Empty : loaded.Error.Message));
        }

        var phases = new[] { CombatPhase.AiDecision, CombatPhase.VisualInfo, CombatPhase.PcDeclaration, CombatPhase.ActionResolution, CombatPhase.PerAttack };
        Assert.Equal(phases, Enum.GetValues<CombatPhase>());
    }
}

public class ValidateGameStateTeamValidationPhase4
{
    private static Result<LoadedSave> WithLineup(params string?[] ids) =>
        ReadEdited(MakeGame(), g => g["lineup"] = new JsonArray([.. ids.Select(i => (JsonNode?)(i is null ? null : JsonValue.Create(i)))]));

    [Fact(DisplayName = "accepts team: [] (empty team)")]
    public void Accepts_empty_lineup()
    {
        var loaded = WithLineup().Value;
        Assert.Empty(loaded.Game.Lineup);
        Assert.True(loaded.Report.IsEmpty);
    }

    [Fact(DisplayName = "accepts team with 2 string entries")]
    public void Accepts_two_entries()
    {
        var loaded = WithLineup(TestCast.ElenaId, TestCast.LarsId).Value;
        Assert.Equal([TestCast.ElenaId, TestCast.LarsId], loaded.Game.Lineup);
        Assert.True(loaded.Report.IsEmpty);
    }

    [Fact(DisplayName = "rejects team with 1 entry")]
    public void One_entry_is_cleared()
    {
        var loaded = WithLineup(TestCast.ElenaId).Value;
        Assert.Empty(loaded.Game.Lineup);
        var adjustment = Assert.Single(loaded.Report.Adjustments);
        Assert.Equal(LoadAdjustmentKind.LineupCleared, adjustment.Kind);
        Assert.Contains("lineup size has changed", adjustment.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "rejects team with 3 entries")]
    public void Three_entries_are_cleared()
    {
        var loaded = WithLineup(TestCast.ElenaId, TestCast.LarsId, TestCast.KadeId).Value;
        Assert.Empty(loaded.Game.Lineup);
        Assert.Equal(LoadAdjustmentKind.LineupCleared, Assert.Single(loaded.Report.Adjustments).Kind);
    }

    [Fact(DisplayName = "rejects team with non-string entries")]
    public void Rejects_non_string_entries()
    {
        Refused(ReadEdited(MakeGame(), g => g["lineup"] = new JsonArray(1, 2)));
        Refused(WithLineup(null, null));
    }

    [Fact(DisplayName = "accepts missing team field (backward compat)")]
    public void Missing_lineup_is_refused()
    {
        var error = Refused(ReadEdited(MakeGame(), g => g.Remove("lineup")));
        Assert.Contains("invalid game data", error.Message, StringComparison.Ordinal);
    }
}

public class ValidateGameStateValidationResultShape
{
    [Fact(DisplayName = "returns { valid: true } for valid state")]
    public void Valid_state()
    {
        var read = Read(WriteOk(MakeGame()));
        Assert.True(read.IsOk);
        Assert.True(read.Value.Report.IsEmpty);
    }

    [Fact(DisplayName = "returns { valid: false, errors: [...] } with accumulated errors for multiple failures")]
    public void Accumulates_errors()
    {
        var doc = Doc(MakeGame());
        doc["kind"] = "auto";
        doc["slot"] = 0;
        var game = (JsonObject)doc["game"]!;
        FormatEdit.SetPersonality(game, 3, 30, 17, 17, 17, 16);
        var error = Refused(Read(doc));
        Assert.True(error.Details.Length > 1);
        Assert.Contains("kind: unsupported", error.Message, StringComparison.Ordinal);
        Assert.Contains("slot: expected integer between 1 and 10", error.Message, StringComparison.Ordinal);
        Assert.Equal(error.Details.Length, error.Message.Split("; ").Length);
    }
}

public class SaveFormatRoundTrip
{
    [Fact(DisplayName = "a mid-fight game loads back identical and re-saves to identical bytes")]
    public void Identical_state_and_bytes()
    {
        var game = FightGame(3);
        var bytes = WriteOk(game, LongFightContent, 7, 123_456);
        var loaded = ReadOk(bytes, LongFightContent);
        StateAssert.Equivalent(game, loaded.Game);
        Assert.Equal(7, loaded.Slot);
        Assert.Equal(123_456, loaded.SavedAt);
        Assert.Equal(game.Player.Name, loaded.PlayerName);
        Assert.True(loaded.Report.IsEmpty);
        Assert.Equal(bytes, WriteOk(loaded.Game, LongFightContent, loaded.Slot, loaded.SavedAt));
    }

    [Fact(DisplayName = "writes UTF-8 without a byte order mark and with plain line feeds")]
    public void Plain_utf8()
    {
        var bytes = WriteOk(FightGame(1), LongFightContent);
        Assert.Equal((byte)'{', bytes[0]);
        Assert.DoesNotContain((byte)'\r', bytes);
        var doc = JsonNode.Parse(bytes)!;
        Assert.Equal("ancient-order-save", (string?)doc["format"]);
        Assert.Equal(1, (int)doc["version"]!);
        Assert.Equal("manual", (string?)doc["kind"]);
    }

    [Fact(DisplayName = "keeps the dice of a mid-fight save")]
    public void Keeps_the_dice()
    {
        var game = FightGame(2);
        var loaded = ReadOk(WriteOk(game, LongFightContent), LongFightContent).Game;
        Assert.Equal(game.Combat!.Dice, loaded.Combat!.Dice);
        Assert.NotEqual(DiceState.FromSeed(TestSessions.Seed), game.Combat.Dice);
    }

    [Fact(DisplayName = "the next rounds of a reloaded fight resolve exactly as without saving")]
    public void Same_dice_after_reload()
    {
        var game = FightGame(2);
        var original = game.Combat!;
        var reloaded = ReadOk(WriteOk(game, LongFightContent), LongFightContent).Game.Combat!;
        var archetypes = LongFightContent.Archetypes;

        for (var round = 0; round < 5; round++)
        {
            var orders = AttackOrders(original);
            original = RoundManager.RunRound(original, orders, archetypes);
            reloaded = RoundManager.RunRound(reloaded, orders, archetypes);
            StateAssert.Equivalent(original, reloaded);
        }

        Assert.Equal(8, original.Round);
    }

    [Fact(DisplayName = "a long fight still saves well under the size limit with empty nested round histories")]
    public void Long_fight_stays_small()
    {
        var game = FightGame(35);
        Assert.Equal(36, game.Combat!.Round);
        Assert.Equal(35, game.Combat.RoundHistory.Length);
        var bytes = WriteOk(game, LongFightContent);
        Assert.True(bytes.Length < SaveConstants.MaxSaveBytes / 2, "bytes: " + bytes.Length);
        StateAssert.Equivalent(game, ReadOk(bytes, LongFightContent).Game);

        var rounds = (JsonArray)JsonNode.Parse(bytes)!["game"]!["combat"]!["roundHistory"]!;
        Assert.Equal(35, rounds.Count);
        Assert.All(rounds, r => Assert.Empty((JsonArray)r!["stateSnapshot"]!["roundHistory"]!));
    }

    [Fact(DisplayName = "mirrors counter exchanges and evade recovery in the round history")]
    public void Mirrors_counter_exchanges_and_evade_recovery()
    {
        var game = FightGame(1);
        var combat = game.Combat!;
        var parried = new DefenseResult(DefenseType.Parry, true, 0.5);
        var withCounters = new ActionResult(combat.PlayerParty[0].Id, ActionType.Attack, null)
        {
            CounterExchanges = [new CounterExchange(combat.EnemyParty[0].Id, combat.PlayerParty[0].Id, DefenseType.Parry, parried, 3.25)],
        };
        var evaded = new ActionResult(combat.PlayerParty[1].Id, ActionType.Evade, null) { EvadeRecovery = 4.5 };
        var round = combat.RoundHistory[0] with { Actions = [withCounters, evaded] };
        var edited = game with { Combat = combat with { RoundHistory = [round] } };

        var loaded = ReadOk(WriteOk(edited, LongFightContent), LongFightContent).Game;
        StateAssert.Equivalent(edited, loaded);
        var actions = loaded.Combat!.RoundHistory[0].Actions;
        Assert.Equal(3.25, actions[0].CounterExchanges[0].Damage);
        Assert.Equal(DefenseType.Parry, actions[0].CounterExchanges[0].Defense);
        Assert.Null(actions[0].EvadeRecovery);
        Assert.Equal(4.5, actions[1].EvadeRecovery);
        Assert.Empty(actions[1].CounterExchanges);
    }
}

public class SaveFormatCultureIndependence
{
    private static GameState GameWithFractions()
    {
        var game = FightGame(3);
        var changed = CharacterRules.ChangeAffection(LongFightContent, game, TestCast.ElenaId, 12.5, 1);
        return CharacterRules.ChangeTrust(LongFightContent, changed.Value, TestCast.LarsId, -7.25, 2).Value;
    }

    [Theory(DisplayName = "saves to identical bytes under every OS language setting")]
    [InlineData("en-US")]
    [InlineData("sv-SE")]
    [InlineData("tr-TR")]
    public void Identical_bytes(string cultureName)
    {
        var game = GameWithFractions();
        var baseline = WriteOk(game, LongFightContent, 4, 99);
        var savedCulture = CultureInfo.CurrentCulture;
        var savedUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;

            var underCulture = WriteOk(game, LongFightContent, 4, 99);
            Assert.Equal(baseline, underCulture);

            var loaded = ReadOk(underCulture, LongFightContent);
            Assert.Equal(baseline, WriteOk(loaded.Game, LongFightContent, loaded.Slot, loaded.SavedAt));
            StateAssert.Equivalent(game, loaded.Game);
        }
        finally
        {
            CultureInfo.CurrentCulture = savedCulture;
            CultureInfo.CurrentUICulture = savedUiCulture;
        }
    }

    [Fact(DisplayName = "a save made under one language setting re-saves to identical bytes under another")]
    public void Made_under_one_loaded_under_another()
    {
        var game = GameWithFractions();
        var savedCulture = CultureInfo.CurrentCulture;
        var savedUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("sv-SE");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("sv-SE");
            var made = WriteOk(game, LongFightContent, 2, 5);

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");
            var loaded = ReadOk(made, LongFightContent);
            Assert.Equal(made, WriteOk(loaded.Game, LongFightContent, loaded.Slot, loaded.SavedAt));

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            Assert.Equal(made, WriteOk(ReadOk(made, LongFightContent).Game, LongFightContent, 2, 5));
        }
        finally
        {
            CultureInfo.CurrentCulture = savedCulture;
            CultureInfo.CurrentUICulture = savedUiCulture;
        }
    }
}

public class SaveFormatSize
{
    private static GameState WithName(int length) => MakeGame(new string('a', length));

    private static GameState WithManyProgressEntries(int count)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, ReactionProgress>(StringComparer.Ordinal);
        for (var i = 0; i < count; i++)
        {
            builder["fighter-" + i.ToString("D8", CultureInfo.InvariantCulture)] = new ReactionProgress(i, i, i);
        }

        return MakeGame() with { ReactionProgress = builder.ToImmutable() };
    }

    [Fact(DisplayName = "refuses at save time a game that would exceed 2,000,000 bytes")]
    public void Refuses_an_oversized_game_on_write()
    {
        var written = SaveFormat.Write(1, SaveConstants.KindManual, WithManyProgressEntries(30_000), Content, 1);
        var error = Refused(written);
        Assert.StartsWith("Save is too large to write (", error.Message, StringComparison.Ordinal);
        Assert.EndsWith(", limit 2000000)", error.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "writes nothing and keeps the old save when the game is too large")]
    public async Task Writes_nothing_when_too_large()
    {
        var backend = new MemorySaveBackend();
        var store = new SaveStore(backend, Content);
        await store.SaveAsync(1, MakeGame("Keep"), 1, Ct);
        var before = await backend.ReadAsync(KeyFor(1), Ct);

        var result = await store.SaveAsync(1, WithManyProgressEntries(30_000), 2, Ct);
        Assert.Equal(ErrorCode.VALIDATION_ERROR, Refused(result).Code);
        Assert.Equal(before, await backend.ReadAsync(KeyFor(1), Ct));
        Assert.Null(await backend.ReadAsync(KeyFor(2), Ct));
    }

    [Fact(DisplayName = "the write limit counts bytes: the largest save that fits is written and one byte more is refused")]
    public void Write_limit_is_exact()
    {
        var one = WriteOk(WithName(1)).Length;
        var fit = 1 + ((SaveConstants.MaxSaveBytes - one) / 2);
        var biggest = WriteOk(WithName(fit));
        Assert.InRange(biggest.Length, SaveConstants.MaxSaveBytes - 1, SaveConstants.MaxSaveBytes);

        var over = SaveFormat.Write(1, SaveConstants.KindManual, WithName(fit + 1), Content, TestContent.Now);
        Refused(over);
    }

    [Fact(DisplayName = "the read limit is 2,000,000 bytes, counted in UTF-8 bytes")]
    public void Read_limit_is_in_bytes()
    {
        var compact = Doc(MakeGame()).ToJsonString();
        var baseBytes = Encoding.UTF8.GetByteCount(compact[..^1]) + Encoding.UTF8.GetByteCount(",\"padding\":\"\"}");
        var pad = SaveConstants.MaxSaveBytes - baseBytes;

        string Padded(int length, char c) => compact[..^1] + ",\"padding\":\"" + new string(c, length) + "\"}";

        var exact = Encoding.UTF8.GetBytes(Padded(pad, 'x'));
        Assert.Equal(SaveConstants.MaxSaveBytes, exact.Length);
        Assert.True(Read(exact).IsOk);

        var over = Encoding.UTF8.GetBytes(Padded(pad + 1, 'x'));
        Assert.Equal(SaveConstants.MaxSaveBytes + 1, over.Length);
        Assert.Contains("save too large", Refused(Read(over)).Message, StringComparison.Ordinal);

        var wide = Padded((pad / 2) + 10, 'é');
        Assert.True(wide.Length < SaveConstants.MaxSaveBytes);
        var wideBytes = Encoding.UTF8.GetBytes(wide);
        Assert.True(wideBytes.Length > SaveConstants.MaxSaveBytes);
        Assert.Contains("save too large", Refused(Read(wideBytes)).Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "a slot below 1 or above 10 is refused at write")]
    public void Invalid_slot_on_write()
    {
        var error = Refused(SaveFormat.Write(0, SaveConstants.KindManual, MakeGame(), Content, 1), ErrorCode.INVALID_SLOT);
        Assert.Equal("Invalid slot number: 0. Must be between 1 and 10.", error.Message);
        Refused(SaveFormat.Write(11, SaveConstants.KindManual, MakeGame(), Content, 1), ErrorCode.INVALID_SLOT);
    }
}

public class SaveFormatStrictReading
{
    [Fact(DisplayName = "never throws on damaged bytes and refuses each as VALIDATION_ERROR")]
    public void Damaged_bytes_are_refused()
    {
        var good = WriteOk(MakeGame());
        var cases = new List<byte[]>
        {
            Array.Empty<byte>(),
            new byte[] { 0 },
            new byte[] { 0xFF, 0xFE, 0x00 },
            Encoding.UTF8.GetBytes("{"),
            Encoding.UTF8.GetBytes("{\"format\":"),
            Encoding.UTF8.GetBytes(new string('[', 100_000)),
            Encoding.UTF8.GetBytes("{\"format\":\"ancient-order-save\",\"version\":1e999}"),
            good[..(good.Length / 2)],
            good.Concat(Encoding.UTF8.GetBytes("garbage")).ToArray(),
        };
        foreach (var bytes in cases)
        {
            Assert.Equal(ErrorCode.VALIDATION_ERROR, Refused(Read(bytes)).Code);
        }
    }

    [Fact(DisplayName = "refuses duplicate property names")]
    public void Refuses_duplicate_keys()
    {
        var text = Doc(MakeGame()).ToJsonString();
        var duplicated = text.Replace("\"kind\":\"manual\"", "\"kind\":\"manual\",\"kind\":\"manual\"", StringComparison.Ordinal);
        Assert.NotEqual(text, duplicated);
        Assert.Equal("invalid JSON", Refused(Read(duplicated)).Message);
    }

    [Fact(DisplayName = "refuses a wrong format tag and a version it cannot read")]
    public void Refuses_tag_and_version()
    {
        Assert.Equal("wrong format tag", Refused(ReadEnvelopeEdited(MakeGame(), e => e["format"] = "other")).Message);
        Assert.Equal("wrong format tag", Refused(ReadEnvelopeEdited(MakeGame(), e => e.Remove("format"))).Message);
        Assert.Equal("wrong format tag", Refused(ReadEnvelopeEdited(MakeGame(), e => e["format"] = 1)).Message);
        Assert.Equal("unsupported version 2", Refused(ReadEnvelopeEdited(MakeGame(), e => e["version"] = 2)).Message);
        Assert.Equal("unsupported version \"1\"", Refused(ReadEnvelopeEdited(MakeGame(), e => e["version"] = "1")).Message);
        Assert.Equal("unsupported version undefined", Refused(ReadEnvelopeEdited(MakeGame(), e => e.Remove("version"))).Message);
        Assert.Equal("unsupported version -1", Refused(ReadEnvelopeEdited(MakeGame(), e => e["version"] = -1)).Message);
    }

    [Theory(DisplayName = "reads enums only as their exact camelCase names")]
    [InlineData("pcDeclaration", true)]
    [InlineData("PcDeclaration", false)]
    [InlineData("pcdeclaration", false)]
    [InlineData("PC_DECLARATION", false)]
    [InlineData("nonsense", false)]
    [InlineData("", false)]
    public void Strict_enum_names(string phase, bool accepted)
    {
        Assert.Equal(accepted, FormatEdit.FightEdited(c => c["phase"] = phase).IsOk);
    }

    [Fact(DisplayName = "refuses enums written as numbers, numeric strings, or undefined values")]
    public void Refuses_numeric_enums()
    {
        Refused(FormatEdit.FightEdited(c => c["phase"] = 2));
        Refused(FormatEdit.FightEdited(c => c["phase"] = "2"));
        Refused(FormatEdit.FightEdited(c => c["phase"] = 99));
        Refused(FormatEdit.FightEdited(c => c["status"] = 0));
        Refused(FormatEdit.FightEdited(c => c["status"] = "Active"));
        Refused(FormatEdit.FightEdited(c => c["playerParty"]![0]!["elementalPath"] = 3));
        Refused(FormatEdit.FightEdited(c => c["playerParty"]![0]!["elementalPath"] = "Fire"));
        Refused(FormatEdit.FightEdited(c => c["playerParty"]![0]!["ascensionLevel"] = 1));
        Assert.True(FormatEdit.FightEdited(c => c["playerParty"]![0]!["ascensionLevel"] = "level1").IsOk);
        Refused(FormatEdit.FightEdited(c => c["playerParty"]![0]!["ascensionLevel"] = "level9"));
    }

    [Fact(DisplayName = "a saved fight must be active")]
    public void Saved_fight_must_be_active()
    {
        var error = Refused(FormatEdit.FightEdited(c => c["status"] = "victory"));
        Assert.Contains("combat.status: a saved fight must be active", error.Message, StringComparison.Ordinal);
        Refused(FormatEdit.FightEdited(c => c["status"] = "defeat"));
    }

    [Fact(DisplayName = "a saved fight needs a non-zero dice state")]
    public void Dice_must_be_non_zero()
    {
        var error = Refused(FormatEdit.FightEdited(c => c["dice"] = new JsonObject { ["s0"] = 0, ["s1"] = 0, ["s2"] = 0, ["s3"] = 0 }));
        Assert.Contains("combat.dice: expected a non-zero dice state", error.Message, StringComparison.Ordinal);
        Refused(FormatEdit.FightEdited(c => c.Remove("dice")));
        Refused(FormatEdit.FightEdited(c => c["dice"]!["s0"] = -1));
    }

    [Fact(DisplayName = "fighter stamina must lie between 0 and its maximum")]
    public void Stamina_range()
    {
        var over = Refused(FormatEdit.FightEdited(c => c["playerParty"]![0]!["stamina"] = 999_999));
        Assert.Contains("is outside 0 to maxStamina", over.Message, StringComparison.Ordinal);
        Refused(FormatEdit.FightEdited(c => c["playerParty"]![0]!["stamina"] = -1));
    }

    [Fact(DisplayName = "ids must be 1 to 64 characters everywhere")]
    public void Id_lengths()
    {
        var sixtyFour = new string('a', 64);
        var sixtyFive = new string('a', 65);

        Assert.True(ReadEdited(MakeGame(), g => g["lineup"] = new JsonArray(sixtyFour)).IsOk);
        Refused(ReadEdited(MakeGame(), g => g["lineup"] = new JsonArray(sixtyFive)));
        Refused(ReadEdited(MakeGame(), g => g["lineup"] = new JsonArray("")));
        Refused(ReadEdited(MakeGame(), g => g["characters"]![0]!["id"] = sixtyFive));
        Refused(ReadEdited(MakeGame(), g => g["characters"]![0]!["id"] = ""));
        Refused(ReadEdited(MakeGame(), g => g["reactionProgress"] = Progress(sixtyFive, 1)));
        Assert.True(ReadEdited(MakeGame(), g => g["reactionProgress"] = Progress(sixtyFour, 1)).IsOk);
        Refused(FormatEdit.FightEdited(c => c["playerParty"]![0]!["id"] = sixtyFive));
        Refused(FormatEdit.FightEdited(c => c["actionQueue"] = new JsonArray(new JsonObject { ["combatantId"] = sixtyFive, ["type"] = "attack", ["targetId"] = "x" })));
    }

    private static JsonArray Progress(string id, double xp) =>
        new(new JsonObject { ["id"] = id, ["block"] = xp, ["dodge"] = 0, ["parry"] = 0 });

    [Fact(DisplayName = "reaction XP must be finite, whole, 0 or more, and at most int.MaxValue")]
    public void Xp_rules()
    {
        Assert.True(ReadEdited(MakeGame(), g => g["reactionProgress"] = Progress("p", 0)).IsOk);
        Assert.True(ReadEdited(MakeGame(), g => g["reactionProgress"] = Progress("p", int.MaxValue)).IsOk);
        Assert.True(ReadEdited(MakeGame(), g => g["reactionProgress"] = Progress("p", 215)).IsOk);

        var negative = Refused(ReadEdited(MakeGame(), g => g["reactionProgress"] = Progress("p", -1)));
        Assert.Contains("reactionProgress.p.block: value -1 is below 0", negative.Message, StringComparison.Ordinal);
        var fraction = Refused(ReadEdited(MakeGame(), g => g["reactionProgress"] = Progress("p", 1.5)));
        Assert.Contains("is not a whole number up to 2147483647", fraction.Message, StringComparison.Ordinal);
        Refused(ReadEdited(MakeGame(), g => g["reactionProgress"] = Progress("p", (double)int.MaxValue + 1)));
        Refused(ReadEdited(MakeGame(), g => g["reactionProgress"] = Progress("p", 1e300)));
        Refused(FormatEdit.FightEdited(c => c["playerParty"]![0]!["reactionProgress"] = new JsonObject { ["block"] = -3, ["dodge"] = 0, ["parry"] = 0 }));
    }

    [Fact(DisplayName = "character relationships must lie between -100 and 100, and ids must be unique")]
    public void Character_checks()
    {
        Assert.True(ReadEdited(MakeGame(), g => g["characters"]![0]!["affection"] = 100).IsOk);
        Assert.True(ReadEdited(MakeGame(), g => g["characters"]![0]!["trust"] = -100).IsOk);
        Refused(ReadEdited(MakeGame(), g => g["characters"]![0]!["affection"] = 100.5));
        Refused(ReadEdited(MakeGame(), g => g["characters"]![0]!["trust"] = -101));

        var duplicate = Refused(ReadEdited(MakeGame(), g => g["characters"]![1]!["id"] = TestCast.ElenaId));
        Assert.Contains("characters: duplicate id " + TestCast.ElenaId, duplicate.Message, StringComparison.Ordinal);
        var progress = new JsonArray(
            new JsonObject { ["id"] = "p", ["block"] = 1, ["dodge"] = 0, ["parry"] = 0 },
            new JsonObject { ["id"] = "p", ["block"] = 2, ["dodge"] = 0, ["parry"] = 0 });
        Refused(ReadEdited(MakeGame(), g => g["reactionProgress"] = progress));
    }

    [Fact(DisplayName = "the slot in the envelope is not compared with the slot it was loaded from")]
    public async Task Slot_is_not_compared()
    {
        var backend = new MemorySaveBackend();
        await backend.WriteAsync(KeyFor(2), WriteOk(MakeGame("Copied"), slot: 1), Ct);
        var loaded = (await new SaveStore(backend, Content).LoadAsync(2, Ct)).Value;
        Assert.Equal(1, loaded.Slot);
        Assert.Equal("Copied", loaded.Game.Player.Name);
    }
}

public class SaveUpgradePipeline
{
    // A synthetic version 0: the lineup was called "team" and reactionProgress did not exist yet.
    private static JsonObject VersionZero(GameState game, params string[] drop)
    {
        var doc = Doc(game);
        doc["version"] = 0;
        var g = (JsonObject)doc["game"]!;
        var lineup = g["lineup"]!.DeepClone();
        g.Remove("lineup");
        g["team"] = lineup;
        g.Remove("reactionProgress");
        foreach (var key in drop)
        {
            g.Remove(key);
        }

        return doc;
    }

    private static readonly SaveUpgradeStep ZeroToOne = new(0, doc =>
    {
        var game = (JsonObject)doc["game"]!;
        if (game.Remove("team", out var team))
        {
            game["lineup"] = team;
        }
        else
        {
            game["lineup"] ??= new JsonArray();
        }

        game["reactionProgress"] ??= new JsonArray();
        if (!game.ContainsKey("combat"))
        {
            game["combat"] = null;
        }
    });

    private static Result<LoadedSave> ReadWith(JsonObject doc, params SaveUpgradeStep[] steps) =>
        SaveFormat.Read(Utf8(doc), Content, [.. steps]);

    [Fact(DisplayName = "the production list has no steps yet and the current version is 1")]
    public void Production_list_is_empty()
    {
        Assert.Empty(SaveUpgrades.Steps);
        Assert.Equal(1, SaveConstants.CurrentVersion);
    }

    [Fact(DisplayName = "upgrades a synthetic version 0 save to the current format")]
    public void Upgrades_version_zero()
    {
        var game = MakeGame() with { Lineup = [TestCast.ElenaId, TestCast.LarsId] };
        var loaded = ReadWith(VersionZero(game), ZeroToOne);
        Assert.True(loaded.IsOk, loaded.IsOk ? string.Empty : loaded.Error.Message);
        StateAssert.Equivalent(game, loaded.Value.Game);
        Assert.True(loaded.Value.Report.IsEmpty);
    }

    [Fact(DisplayName = "an upgraded save re-saves as version 1, identical to a save made in the current format")]
    public void Upgraded_save_resaves_as_current()
    {
        var game = MakeGame() with { Lineup = [TestCast.ElenaId, TestCast.LarsId] };
        var loaded = ReadWith(VersionZero(game), ZeroToOne).Value;
        var resaved = WriteOk(loaded.Game, slot: loaded.Slot, now: loaded.SavedAt);
        Assert.Equal(1, (int)JsonNode.Parse(resaved)!["version"]!);
        Assert.Equal(WriteOk(game, slot: loaded.Slot, now: loaded.SavedAt), resaved);
    }

    [Fact(DisplayName = "an older save without the step is refused as an unsupported version")]
    public void No_step_means_refused()
    {
        Assert.Equal("unsupported version 0", Refused(SaveFormat.Read(Utf8(VersionZero(MakeGame())), Content)).Message);
        Assert.Equal("unsupported version 0", Refused(ReadWith(VersionZero(MakeGame()), new SaveUpgradeStep(5, _ => { }))).Message);
    }

    [Fact(DisplayName = "a step that fails refuses the save without throwing")]
    public void Failing_step_is_refused()
    {
        var failing = new SaveUpgradeStep(0, _ => throw new InvalidOperationException("boom"));
        Assert.Equal("upgrade from version 0 failed", Refused(ReadWith(VersionZero(MakeGame()), failing)).Message);
        var unsound = new SaveUpgradeStep(0, doc => doc["game"]!.AsObject().Remove("player"));
        Refused(ReadWith(VersionZero(MakeGame()), unsound));
    }

    [Fact(DisplayName = "runs steps in sequence, setting the version as it goes")]
    public void Runs_steps_in_sequence()
    {
        var seen = new List<string>();
        var steps = ImmutableArray.Create(
            new SaveUpgradeStep(2, doc => seen.Add("2:" + doc["version"]!.ToJsonString())),
            new SaveUpgradeStep(0, doc => seen.Add("0:" + doc["version"]!.ToJsonString())),
            new SaveUpgradeStep(1, doc => seen.Add("1:" + doc["version"]!.ToJsonString())));
        var doc = new JsonObject { ["version"] = 0 };

        Assert.Null(SaveUpgrades.Run(doc, 0, 3, steps));
        Assert.Equal(["0:0", "1:1", "2:2"], seen);
        Assert.Equal("3", doc["version"]!.ToJsonString());

        var gap = new JsonObject { ["version"] = 0 };
        var missing = SaveUpgrades.Run(gap, 0, 3, [new SaveUpgradeStep(0, _ => { })]);
        Assert.Equal("unsupported version 1", missing);
    }

    [Fact(DisplayName = "missing narrativeState loads as null")]
    public void Missing_combat_loads_as_no_fight()
    {
        var loaded = ReadWith(VersionZero(MakeGame(), "combat"), ZeroToOne).Value;
        Assert.Null(loaded.Game.Combat);
    }

    [Fact(DisplayName = "missing team loads as []")]
    public void Missing_team_loads_empty()
    {
        var doc = VersionZero(MakeGame() with { Lineup = [TestCast.ElenaId, TestCast.LarsId] });
        ((JsonObject)doc["game"]!).Remove("team");
        var loaded = ReadWith(doc, ZeroToOne).Value;
        Assert.Empty(loaded.Game.Lineup);
    }

    [Fact(DisplayName = "missing reactionProgress loads as {}")]
    public void Missing_progress_loads_empty()
    {
        var loaded = ReadWith(VersionZero(MakeGame()), ZeroToOne).Value;
        Assert.Empty(loaded.Game.ReactionProgress);
    }

    [Fact(DisplayName = "all three missing together load with defaults")]
    public void All_missing_load_with_defaults()
    {
        var doc = VersionZero(MakeGame(), "combat");
        ((JsonObject)doc["game"]!).Remove("team");
        var loaded = ReadWith(doc, ZeroToOne).Value;
        Assert.Null(loaded.Game.Combat);
        Assert.Empty(loaded.Game.Lineup);
        Assert.Empty(loaded.Game.ReactionProgress);
    }

    [Fact(DisplayName = "an older save still lists as saved")]
    public void Older_save_still_reads_ok()
    {
        var loaded = ReadWith(VersionZero(MakeGame("Old")), ZeroToOne);
        Assert.True(loaded.IsOk);
        Assert.Equal("Old", loaded.Value.PlayerName);
        Assert.Equal(1, loaded.Value.Slot);
    }
}
