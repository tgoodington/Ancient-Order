using System.Globalization;
using AncientOrder.Rules.Saves;
using AncientOrder.Rules.Saves.Format;
using Godot;

namespace AncientOrder.Ui;

/// <summary>
/// One save slot on the save and load screen: a toggle button (so the row can be focused, clicked and selected) over plain labels.
/// Player and town names come from the save file, so they only ever go into plain Labels.
/// </summary>
public partial class SlotRow : Button
{
    private static readonly Color DamagedColor = new(0.7098f, 0.4157f, 0.4157f);

    private Label _number = null!;
    private Label _name = null!;
    private Label _detail = null!;
    private Label _when = null!;

    public int Slot { get; private set; }

    public SlotStatus Status { get; private set; } = SlotStatus.Empty;

    public override void _Ready()
    {
        _number = GetNode<Label>("Margin/Row/Number");
        _name = GetNode<Label>("Margin/Row/Body/Name");
        _detail = GetNode<Label>("Margin/Row/Body/Detail");
        _when = GetNode<Label>("Margin/Row/When");
    }

    /// <summary>Draws the slot. A null slot is the placeholder shown until the first list arrives.</summary>
    public void Show(int slot, SlotInfo? info)
    {
        Slot = slot;
        Status = info?.Status ?? SlotStatus.Empty;
        _number.Text = slot.ToString("00", CultureInfo.CurrentCulture);
        _name.RemoveThemeColorOverride("font_color");
        _name.ThemeTypeVariation = string.Empty;
        _when.Text = string.Empty;
        _detail.Text = string.Empty;
        _detail.Visible = false;
        switch (info)
        {
            case null:
                _name.Text = "...";
                _name.ThemeTypeVariation = "MutedLabel";
                break;
            case { Status: SlotStatus.Empty }:
                _name.Text = "Empty";
                _name.ThemeTypeVariation = "MutedLabel";
                break;
            case { Status: SlotStatus.Corrupted }:
                _name.Text = "Damaged save";
                _name.AddThemeColorOverride("font_color", DamagedColor);
                _detail.Text = "This save can't be read. It can be deleted.";
                _detail.Visible = true;
                break;
            default:
                Fill(info.Metadata);
                break;
        }

        AccessibilityName = $"Slot {slot}: {_name.Text}";
    }

    private void Fill(SaveMetadata? meta)
    {
        if (meta is null)
        {
            _name.Text = "Saved game";
            return;
        }

        _name.Text = string.IsNullOrWhiteSpace(meta.PlayerName) ? "Unnamed" : meta.PlayerName;
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(meta.TownName))
        {
            parts.Add(meta.TownName);
        }

        var situation = SituationWords(meta.Situation);
        if (situation is not null)
        {
            parts.Add(situation);
        }

        _detail.Text = string.Join("  -  ", parts);
        _detail.Visible = parts.Count > 0;
        _when.Text = SavedWhen(meta.SavedAt);
    }

    public static string? SituationWords(SaveSituation situation) => situation switch
    {
        SaveSituation.Town => "In town",
        SaveSituation.Conversation => "In a conversation",
        SaveSituation.HandOff => "Ready for the fight",
        _ => null,
    };

    /// <summary>The saved time (Unix milliseconds) in the player's local time and culture.</summary>
    public static string SavedWhen(long unixMilliseconds)
    {
        try
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds).ToLocalTime().DateTime.ToString("g", CultureInfo.CurrentCulture);
        }
        catch (ArgumentOutOfRangeException)
        {
            return string.Empty;
        }
    }
}
