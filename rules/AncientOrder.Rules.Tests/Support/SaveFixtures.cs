using System.Collections.Immutable;
using System.Text;
using System.Text.Json.Nodes;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Saves;
using AncientOrder.Rules.Session;
using AncientOrder.Rules.State;
using AncientOrder.Rules.Tests.Fixtures;
using Microsoft.Extensions.Time.Testing;

namespace AncientOrder.Rules.Tests.Support;

/// <summary>Shared building blocks for the save tests: content, games, save documents, flaky backends, and sessions over a store.</summary>
public static class SaveFixtures
{
    public const string LongEncounterId = "encounter_long";

    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static GameContent Content { get; } = TestSessions.Content();

    /// <summary>Hero and enemies with so much stamina and so little power that a fight never ends within a test.</summary>
    public static GameContent LongFightContent { get; } = BuildLongFightContent();

    public static string KeyFor(int slot) => SaveStore.KeyFor(slot);

    public static GameState MakeGame(string playerName = "Rin", GameContent? content = null)
    {
        var game = TestContent.NewGame(content ?? Content);
        return game with { Player = game.Player with { Name = playerName } };
    }

    /// <summary>A session over the long-fight content with a new game, a full lineup, and the long encounter started.</summary>
    public static GameSession FightSession(ISaveBackend? backend = null, ISeedSource? seeds = null)
    {
        var session = SessionOver(backend ?? new MemorySaveBackend(), LongFightContent, seeds: seeds);
        Assert.True(session.NewGame().IsOk);
        Assert.True(session.SetLineup(TestSessions.Pair).IsOk);
        var started = session.StartEncounter(LongEncounterId);
        Assert.True(started.IsOk, started.IsOk ? string.Empty : started.Error.Message);
        return session;
    }

    /// <summary>A fight on the long-fight content, played for the given number of rounds (zero gives a fresh fight).</summary>
    public static GameState FightGame(int rounds = 0)
    {
        var session = FightSession();
        if (rounds > 0)
        {
            Assert.Equal(CombatStatus.Active, QuickFight.FightToEnd(session, rounds));
        }

        return session.Snapshot.Game!;
    }

    /// <summary>Attack orders from every fighter on the player side at the first enemy.</summary>
    public static ImmutableArray<CombatAction> AttackOrders(CombatState combat) =>
        [.. combat.PlayerParty.Select(p => TestSessions.Attack(p.Id, combat.EnemyParty[0].Id))];

    public static byte[] WriteOk(GameState game, GameContent? content = null, int slot = 1, long now = TestContent.Now)
    {
        var written = SaveFormat.Write(slot, SaveConstants.KindManual, game, content ?? Content, now);
        Assert.True(written.IsOk, written.IsOk ? string.Empty : written.Error.Message);
        return written.Value;
    }

    public static LoadedSave ReadOk(byte[] bytes, GameContent? content = null)
    {
        var read = SaveFormat.Read(bytes, content ?? Content);
        Assert.True(read.IsOk, read.IsOk ? string.Empty : read.Error.Message);
        return read.Value;
    }

    public static Result<LoadedSave> Read(byte[] bytes, GameContent? content = null) => SaveFormat.Read(bytes, content ?? Content);

    public static Result<LoadedSave> Read(string text, GameContent? content = null) => SaveFormat.Read(Encoding.UTF8.GetBytes(text), content ?? Content);

    /// <summary>The save document of a game as a mutable JSON object.</summary>
    public static JsonObject Doc(GameState game, GameContent? content = null) =>
        (JsonObject)JsonNode.Parse(WriteOk(game, content))!;

    public static byte[] Utf8(JsonNode node) => Encoding.UTF8.GetBytes(node.ToJsonString());

    public static Result<LoadedSave> Read(JsonNode doc, GameContent? content = null) => Read(Utf8(doc), content);

    /// <summary>Applies the edit to the "game" object of a fresh save document and reads the result.</summary>
    public static Result<LoadedSave> ReadEdited(GameState game, Action<JsonObject> edit, GameContent? content = null)
    {
        var doc = Doc(game, content);
        edit((JsonObject)doc["game"]!);
        return Read(doc, content);
    }

    public static Result<LoadedSave> ReadEnvelopeEdited(GameState game, Action<JsonObject> edit)
    {
        var doc = Doc(game);
        edit(doc);
        return Read(doc);
    }

    public static RuleError Refused<T>(Result<T> result, ErrorCode code = ErrorCode.VALIDATION_ERROR)
    {
        Assert.False(result.IsOk, "expected a refusal");
        Assert.Equal(code, result.Error.Code);
        return result.Error;
    }

    public static GameSession SessionOver(
        ISaveBackend backend,
        GameContent? content = null,
        FakeTimeProvider? clock = null,
        ISeedSource? seeds = null)
    {
        var useContent = content ?? Content;
        return new GameSession(
            useContent,
            new GameSessionOptions
            {
                TimeProvider = clock ?? new FakeTimeProvider(DateTimeOffset.FromUnixTimeMilliseconds(TestContent.Now)),
                IdSource = new FixedIdSource("fixed"),
                SeedSource = seeds ?? new FixedSeedSource(TestSessions.Seed),
                Saves = new SaveStore(backend, useContent),
            });
    }

    public static GameSession SessionWithGame(ISaveBackend? backend = null, GameContent? content = null)
    {
        var session = SessionOver(backend ?? new MemorySaveBackend(), content);
        Assert.True(session.NewGame().IsOk);
        return session;
    }

    private static GameContent BuildLongFightContent()
    {
        static CombatProfile Tough(CombatProfile p) => p with { Stamina = 100_000, Power = 1 };
        var characters = TestCast.All.Select(c => c with { Combat = Tough(c.Combat!) }).ToImmutableArray();
        var enemies = SessionEncounters.TestEncounter.Enemies
            .Select(e => e with { Rank = 3, Stamina = 100_000, Power = 1 })
            .ToImmutableArray();
        return TestSessions.Content(
            Tough(TestSessions.HeroProfile),
            characters,
            [new EncounterDefinition(LongEncounterId, "Long fight", enemies)]);
    }
}

/// <summary>Wraps a backend and throws SaveStorageException from the operations flagged to fail. Reading also covers ExistsAsync.</summary>
public sealed class FlakyBackend(ISaveBackend inner) : ISaveBackend
{
    public string? FailRead { get; set; }

    public string? FailWrite { get; set; }

    public string? FailRemove { get; set; }

    public ValueTask<byte[]?> ReadAsync(string key, CancellationToken cancellationToken = default) =>
        FailRead is null ? inner.ReadAsync(key, cancellationToken) : throw new SaveStorageException(FailRead);

    public ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
        FailRead is null ? inner.ExistsAsync(key, cancellationToken) : throw new SaveStorageException(FailRead);

    public ValueTask WriteAsync(string key, byte[] bytes, CancellationToken cancellationToken = default) =>
        FailWrite is null ? inner.WriteAsync(key, bytes, cancellationToken) : throw new SaveStorageException(FailWrite);

    public ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        FailRemove is null ? inner.RemoveAsync(key, cancellationToken) : throw new SaveStorageException(FailRemove);
}
