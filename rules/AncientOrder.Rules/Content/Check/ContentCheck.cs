using AncientOrder.Rules.Common;
using AncientOrder.Rules.Content.Packs;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Content.Check;

/// <summary>
/// The one check over a whole content pack: the loader's own checks, then (when the pack loads) the whole-pack analyses.
/// Every finding names its file and JSON location. Findings are in content order, so output is the same on every machine.
/// </summary>
public static class ContentCheck
{
    public static ContentCheckReport Run(IContentSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var loaded = ContentPackLoader.Load(source, out var provenance);
        if (!loaded.IsOk)
        {
            var details = loaded.Error.Details.IsDefaultOrEmpty
                ? [new RuleErrorDetail(ContentPackLoader.IndexFileName, loaded.Error.Message)]
                : loaded.Error.Details;
            return new ContentCheckReport(null, details, []);
        }

        var content = loaded.Value;
        var problems = new List<RuleErrorDetail>();
        var warnings = new List<RuleErrorDetail>();
        Analyze(content, provenance, problems, warnings);
        return new ContentCheckReport(problems.Count == 0 ? content : null, [.. problems], [.. warnings]);
    }

    private static void Analyze(GameContent content, ContentProvenance provenance, List<RuleErrorDetail> problems, List<RuleErrorDetail> warnings)
    {
        DeadEndCheck.Run(content, provenance, problems);
        GroundCheck.Run(content, provenance, problems);
        FlagCheck.Run(content, provenance, problems, warnings);
        TextCheck.Run(content, provenance, problems, warnings);
        ReachabilityCheck.Run(content, provenance, warnings);
    }
}
