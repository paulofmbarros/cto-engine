using System.Globalization;
using System.Text;
using Cto.Core.Common;

namespace Cto.Core.Planning;

public sealed class PlanBundleService
{
    private readonly string _engineRoot;

    public PlanBundleService(string engineRoot)
    {
        _engineRoot = engineRoot;
    }

    public async Task<OperationResult> GenerateAsync(ProjectPaths paths, CancellationToken cancellationToken = default)
    {
        var requiredFiles = new[]
        {
            paths.CharterPath,
            paths.ContextPath,
            paths.WeeklyLogPath,
            paths.RealityCheckPath,
            paths.ChallengeLogPath,
        };

        foreach (var file in requiredFiles)
        {
            if (!File.Exists(file))
            {
                return OperationResult.Fail($"Missing required planning input: {file}");
            }
        }

        var now = DateTime.UtcNow;
        var isoWeek = ISOWeek.GetWeekOfYear(now);
        var proposalDir = Path.Combine(paths.ProposalsDirectory, $"{now.Year}-W{isoWeek:D2}");
        Directory.CreateDirectory(proposalDir);

        var charter = await File.ReadAllTextAsync(paths.CharterPath, cancellationToken);
        var context = await File.ReadAllTextAsync(paths.ContextPath, cancellationToken);
        var weeklylog = await File.ReadAllTextAsync(paths.WeeklyLogPath, cancellationToken);
        var realityCheck = await File.ReadAllTextAsync(paths.RealityCheckPath, cancellationToken);
        var challengeLog = await File.ReadAllTextAsync(paths.ChallengeLogPath, cancellationToken);

        var sharedInputs = BuildSharedInputBlock(charter, context, weeklylog, realityCheck, challengeLog);

        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["cto.prompt.md"] = BuildPrompt("prompts/cto/weekly-planning.prompt", "CTO", sharedInputs),
            ["architect.prompt.md"] = BuildPrompt("prompts/architect/architecture-review.prompt", "Architect", sharedInputs),
            ["risk.prompt.md"] = BuildPrompt("prompts/risk/risk-review.prompt", "Risk", sharedInputs),
            ["execution.prompt.md"] = BuildPrompt("prompts/execution/decomposition.prompt", "Execution", sharedInputs),
            ["proposal.md"] = BuildProposalScaffold(),
        };

        foreach (var file in files)
        {
            var outputPath = Path.Combine(proposalDir, file.Key);
            await File.WriteAllTextAsync(outputPath, file.Value, cancellationToken);
        }

        return OperationResult.Ok($"Planning bundle generated at {proposalDir}");
    }

    private string BuildPrompt(string templateRelativePath, string roleName, string sharedInput)
    {
        var templatePath = Path.Combine(_engineRoot, templateRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var template = File.Exists(templatePath)
            ? File.ReadAllText(templatePath)
            : $"# {roleName} Prompt\nAnalyze the provided inputs and produce role-specific recommendations.";

        return $"{template.Trim()}\n\n---\n\n{sharedInput}";
    }

    private static string BuildSharedInputBlock(string charter, string context, string weeklylog, string realityCheck, string challengeLog)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Inputs");
        builder.AppendLine();
        AppendSection(builder, "Charter", charter);
        AppendSection(builder, "Context", context);
        AppendSection(builder, "Weekly Log", weeklylog);
        AppendSection(builder, "Reality Check", realityCheck);
        AppendSection(builder, "Challenge Log", challengeLog);
        return builder.ToString().TrimEnd();
    }

    private static void AppendSection(StringBuilder builder, string title, string content)
    {
        builder.AppendLine($"## {title}");
        builder.AppendLine(content.Trim());
        builder.AppendLine();
    }

    private static string BuildProposalScaffold()
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Proposal");
        builder.AppendLine();
        builder.AppendLine("## Consolidated Direction");
        builder.AppendLine("- _Summarize chosen strategy._");
        builder.AppendLine();
        builder.AppendLine("## Recommended Work Breakdown");
        builder.AppendLine("- _List epics/stories and sequencing._");
        builder.AppendLine();
        builder.AppendLine("## Risks and Mitigations");
        builder.AppendLine("- _Capture principal risks and guardrails._");
        builder.AppendLine();
        builder.AppendLine("## Open Questions for Human Decision");
        builder.AppendLine("- _List final decisions needed before plan approval._");
        return builder.ToString();
    }
}
