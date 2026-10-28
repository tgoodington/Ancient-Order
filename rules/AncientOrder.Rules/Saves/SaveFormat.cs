using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Saves.Format;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Saves;

/// <summary>A sound save, loaded: the slot, time, and name from its envelope, the game fitted to the current content, and what was adjusted.</summary>
public sealed record LoadedSave(int Slot, long SavedAt, string PlayerName, GameState Game, LoadReport Report);

public static class SaveFormat
{
    private static SaveJsonContext Context => SaveJsonContext.Strict;

    /// <summary>Checks the game, maps it to the format records, and serializes it to UTF-8. Refuses a bad slot, an invalid game, or more than 2,000,000 bytes; nothing is produced then.</summary>
    public static Result<byte[]> Write(int slot, string kind, GameState game, GameContent content, long now)
    {
        if (!SaveConstants.IsValidSlot(slot))
        {
            return Result<byte[]>.Fail(new RuleError(
                ErrorCode.INVALID_SLOT,
                $"Invalid slot number: {slot.ToString(CultureInfo.InvariantCulture)}. Must be between {SaveConstants.MinSlot.ToString(CultureInfo.InvariantCulture)} and {SaveConstants.MaxSlot.ToString(CultureInfo.InvariantCulture)}."));
        }

        var saved = SaveMapper.ToSaved(game, content);
        var envelope = new SaveEnvelope(SaveConstants.FormatTag, SaveConstants.CurrentVersion, kind, slot, now, game.Player.Name, saved);

        var reasons = new List<string>();
        SaveChecks.CheckEnvelope(envelope, reasons);
        if (reasons.Count > 0)
        {
            return Result<byte[]>.Fail(Refusal("Game state is invalid: ", reasons));
        }

        byte[] bytes;
        try
        {
            bytes = JsonSerializer.SerializeToUtf8Bytes(envelope, Context.SaveEnvelope);
        }
        catch (JsonException)
        {
            return Result<byte[]>.Fail(new RuleError(ErrorCode.VALIDATION_ERROR, "Save is too large to write (too deeply nested)"));
        }

        if (bytes.Length > SaveConstants.MaxSaveBytes)
        {
            return Result<byte[]>.Fail(new RuleError(
                ErrorCode.VALIDATION_ERROR,
                $"Save is too large to write ({bytes.Length.ToString(CultureInfo.InvariantCulture)} bytes, limit {SaveConstants.MaxSaveBytes.ToString(CultureInfo.InvariantCulture)})"));
        }

        return Result<byte[]>.Ok(bytes);
    }

    /// <summary>
    /// Never throws. Order: size, JSON, object, format tag, version (upgrading older saves), shape, value checks, reconcile.
    /// A refusal is VALIDATION_ERROR whose message and details list the reasons (at most 10, each at most 200 characters).
    /// </summary>
    [SuppressMessage("Design", "CA1031", Justification = "Read must never throw; anything unexpected is reported as a damaged save.")]
    public static Result<LoadedSave> Read(ReadOnlySpan<byte> bytes, GameContent content, ImmutableArray<SaveUpgradeStep> upgrades = default)
    {
        try
        {
            return ReadCore(bytes, content, upgrades.IsDefault ? SaveUpgrades.Steps : upgrades);
        }
        catch (Exception)
        {
            return Fail("invalid game data");
        }
    }

    [SuppressMessage("Design", "CA1031", Justification = "Any parse failure, including duplicate property names, means the text is not usable JSON.")]
    private static Result<LoadedSave> ReadCore(ReadOnlySpan<byte> bytes, GameContent content, ImmutableArray<SaveUpgradeStep> upgrades)
    {
        if (bytes.Length > SaveConstants.MaxSaveBytes)
        {
            return Fail("save too large");
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(bytes, null, new JsonDocumentOptions { MaxDepth = SaveConstants.MaxJsonDepth, AllowDuplicateProperties = false });
        }
        catch (Exception)
        {
            return Fail("invalid JSON");
        }

        if (node is not JsonObject root)
        {
            return Fail("save envelope must be an object");
        }

        if (!root.TryGetPropertyValue("format", out var tag) || tag is not JsonValue tagValue
            || !tagValue.TryGetValue<string>(out var tagText) || tagText != SaveConstants.FormatTag)
        {
            return Fail("wrong format tag");
        }

        root.TryGetPropertyValue("version", out var versionNode);
        if (versionNode is not JsonValue versionValue || !versionValue.TryGetValue<long>(out var version) || version > SaveConstants.CurrentVersion)
        {
            return Fail("unsupported version " + Describe(versionNode));
        }

        if (version < SaveConstants.CurrentVersion)
        {
            var upgradeFailure = SaveUpgrades.Run(root, version, SaveConstants.CurrentVersion, upgrades);
            if (upgradeFailure is not null)
            {
                return Fail(upgradeFailure);
            }
        }

        SaveEnvelope? envelope;
        try
        {
            envelope = root.Deserialize(Context.SaveEnvelope);
        }
        catch (JsonException ex)
        {
            return Fail("invalid game data" + (string.IsNullOrEmpty(ex.Path) ? string.Empty : " at " + ex.Path));
        }

        if (envelope is null)
        {
            return Fail("invalid game data");
        }

        var reasons = new List<string>();
        SaveChecks.CheckEnvelope(envelope, reasons);
        if (reasons.Count > 0)
        {
            return Fail(reasons);
        }

        var reconciled = SaveReconciler.Reconcile(envelope.Game, content);
        return Result<LoadedSave>.Ok(new LoadedSave(envelope.Slot, envelope.SavedAt, envelope.PlayerName, reconciled.Game, reconciled.Report));
    }

    private static string Describe(JsonNode? node) =>
        node is null ? "undefined" : node.ToJsonString();

    private static Result<LoadedSave> Fail(string reason) => Fail([reason]);

    // Reasons can echo attacker-controlled text, so their count and length are capped.
    private static Result<LoadedSave> Fail(IReadOnlyList<string> reasons) =>
        Result<LoadedSave>.Fail(Refusal(string.Empty, reasons));

    private static RuleError Refusal(string prefix, IReadOnlyList<string> reasons)
    {
        var capped = reasons
            .Take(SaveConstants.MaxReasons)
            .Select(r => r.Length > SaveConstants.MaxReasonLength ? r[..SaveConstants.MaxReasonLength] : r)
            .ToImmutableArray();
        var message = prefix + string.Join("; ", capped);
        if (message.Length > SaveConstants.MaxReasonLength * SaveConstants.MaxReasons)
        {
            message = message[..(SaveConstants.MaxReasonLength * SaveConstants.MaxReasons)];
        }

        return new RuleError(
            ErrorCode.VALIDATION_ERROR,
            message,
            [.. capped.Select(r => new RuleErrorDetail(string.Empty, r))]);
    }
}
