using AncientOrder.Rules.Common;
using AncientOrder.Rules.Content.Packs;
using AncientOrder.Rules.Reactions;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Content.Check;

/// <summary>
/// The words a player sees. Problems: empty or whitespace-only lines, choices, locked lines, reaction lines, and feel lines; a locked choice with
/// nothing to show (no lockedText of its own and a blank pack default). Warnings: a digit in conversation text (players do not see numbers, ADR-075),
/// and a synergy bonus with no feel line while synergy numbers are hidden (players would not notice it).
/// </summary>
internal static class TextCheck
{
    public static void Run(GameContent content, ContentProvenance provenance, List<RuleErrorDetail> problems, List<RuleErrorDetail> warnings)
    {
        var packLocked = content.Settings.Conversations.LockedText;
        var packLockedBlank = string.IsNullOrWhiteSpace(packLocked);

        for (var c = 0; c < content.Conversations.Definitions.Length; c++)
        {
            var conversation = content.Conversations.Definitions[c];
            for (var s = 0; s < conversation.Steps.Length; s++)
            {
                var step = conversation.Steps[s];
                var sp = "conversations/" + PackWalk.Num(c) + "/steps/" + PackWalk.Num(s);
                var where = $"conversation '{conversation.Id}' step '{step.Id}'";
                for (var l = 0; !step.Lines.IsDefault && l < step.Lines.Length; l++)
                {
                    Words(step.Lines[l].Text, sp + "/lines/" + PackWalk.Num(l) + "/text", $"{where} line {PackWalk.Num(l)}", conversationText: true, provenance, problems, warnings);
                }

                for (var k = 0; !step.Choices.IsDefault && k < step.Choices.Length; k++)
                {
                    var choice = step.Choices[k];
                    var kp = sp + "/choices/" + PackWalk.Num(k);
                    var choiceWhere = $"{where} choice '{choice.Id}'";
                    Words(choice.Text, kp + "/text", choiceWhere + " text", conversationText: true, provenance, problems, warnings);
                    if (choice.LockedText is not null)
                    {
                        Words(choice.LockedText, kp + "/lockedText", choiceWhere + " locked line", conversationText: true, provenance, problems, warnings);
                    }
                    else if (!choice.Requires.IsDefaultOrEmpty && packLockedBlank)
                    {
                        problems.Add(new RuleErrorDetail(
                            provenance.Translate(kp),
                            $"{choiceWhere} has requires but no lockedText, and the pack sets no default locked line (settings conversations.lockedText), so a locked choice would show nothing"));
                    }

                    for (var r = 0; !choice.Reactions.IsDefault && r < choice.Reactions.Length; r++)
                    {
                        var over = choice.Reactions[r];
                        if (!over.Silence && over.Text is not null)
                        {
                            Words(over.Text, kp + "/reactions/" + PackWalk.Num(r) + "/text", $"{choiceWhere} reaction of '{over.CharacterId}'", conversationText: true, provenance, problems, warnings);
                        }
                    }
                }
            }
        }

        for (var a = 0; a < content.Archetypes.Definitions.Length; a++)
        {
            var archetype = content.Archetypes.Definitions[a];
            if (archetype.Reactions is not { } reactions)
            {
                continue;
            }

            var ap = "archetypes/" + PackWalk.Num(a) + "/reactions";
            Lines(reactions.Approve, ap + "/approve", $"archetype '{archetype.Id}' approve", provenance, problems, warnings);
            Lines(reactions.Disapprove, ap + "/disapprove", $"archetype '{archetype.Id}' disapprove", provenance, problems, warnings);
        }

        for (var b = 0; !content.Synergy.Bonuses.IsDefault && b < content.Synergy.Bonuses.Length; b++)
        {
            var bonus = content.Synergy.Bonuses[b];
            var bp = "synergy/bonuses/" + PackWalk.Num(b) + "/feel";
            if (bonus.Feel is not null)
            {
                Words(bonus.Feel, bp, $"synergy bonus '{bonus.Name}' feel line", conversationText: false, provenance, problems, warnings);
            }
            else if (!content.Settings.Display.SynergyNumbers)
            {
                warnings.Add(new RuleErrorDetail(
                    provenance.Translate("synergy/bonuses/" + PackWalk.Num(b)),
                    $"synergy bonus '{bonus.Name}' has no feel line while synergy numbers are hidden, so players would not notice it"));
            }
        }
    }

    private static void Lines(ReactionLines? lines, string path, string what, ContentProvenance provenance, List<RuleErrorDetail> problems, List<RuleErrorDetail> warnings)
    {
        if (lines is null)
        {
            return;
        }

        Words(lines.Clear, path + "/clear", what + " clear line", conversationText: true, provenance, problems, warnings);
        if (lines.Slight is not null)
        {
            Words(lines.Slight, path + "/slight", what + " slight line", conversationText: true, provenance, problems, warnings);
        }

        if (lines.Strong is not null)
        {
            Words(lines.Strong, path + "/strong", what + " strong line", conversationText: true, provenance, problems, warnings);
        }
    }

    private static void Words(string? text, string path, string what, bool conversationText, ContentProvenance provenance, List<RuleErrorDetail> problems, List<RuleErrorDetail> warnings)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            problems.Add(new RuleErrorDetail(provenance.Translate(path), $"{what} is empty"));
        }
        else if (conversationText && text.Any(char.IsAsciiDigit))
        {
            warnings.Add(new RuleErrorDetail(provenance.Translate(path), $"{what} contains a digit; players should not see numbers in conversations"));
        }
    }
}
