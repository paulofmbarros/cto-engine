using System.Globalization;
using Cto.Core.Common;
using Cto.Core.Validation;

namespace Cto.Core.Planning;

public sealed class PlanSelectionService
{
    private readonly LlmConfigValidator _llmConfigValidator = new();

    public async Task<OperationResult> SelectAsync(
        ProjectPaths paths,
        int candidateIndex,
        CancellationToken cancellationToken = default)
    {
        if (candidateIndex < 1)
        {
            return OperationResult.Fail("Candidate index must be >= 1.");
        }

        var (configResult, config) = await _llmConfigValidator.ValidateAsync(paths.LlmConfigPath, cancellationToken);
        if (!configResult.IsValid || config is null)
        {
            var messages = configResult.Issues.Select(i => i.ToString()).ToList();
            messages.Insert(0, "Plan selection blocked: llm-config validation failed.");
            return OperationResult.Fail(messages);
        }

        var proposalDirectory = ResolveProposalDirectory(paths.Root, config.Output, DateTime.UtcNow);
        var candidateFile = config.Output.CandidateFilePattern
            .Replace("{n}", candidateIndex.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        var candidatePath = Path.Combine(proposalDirectory, candidateFile);

        if (!File.Exists(candidatePath))
        {
            return OperationResult.Fail($"Candidate file not found: {candidatePath}");
        }

        File.Copy(candidatePath, paths.PlanPath, overwrite: true);
        return OperationResult.Ok(
            $"Selected candidate {candidateIndex}.",
            $"Copied {candidatePath}",
            $"Updated {paths.PlanPath}");
    }

    public async Task<OperationResult> ListAsync(ProjectPaths paths, CancellationToken cancellationToken = default)
    {
        var (configResult, config) = await _llmConfigValidator.ValidateAsync(paths.LlmConfigPath, cancellationToken);
        if (!configResult.IsValid || config is null)
        {
            var messages = configResult.Issues.Select(i => i.ToString()).ToList();
            messages.Insert(0, "List candidates blocked: llm-config validation failed.");
            return OperationResult.Fail(messages);
        }

        var proposalDirectory = ResolveProposalDirectory(paths.Root, config.Output, DateTime.UtcNow);
        if (!Directory.Exists(proposalDirectory))
        {
            return OperationResult.Fail($"No proposals directory for current week: {proposalDirectory}");
        }

        var candidateFiles = Directory.EnumerateFiles(proposalDirectory, "*.plan.yaml", SearchOption.TopDirectoryOnly)
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidateFiles.Count == 0)
        {
            return OperationResult.Fail($"No candidate plan files found in {proposalDirectory}");
        }

        var result = OperationResult.Ok($"Candidates for current week in {proposalDirectory}:");
        foreach (var file in candidateFiles)
        {
            result.AddMessage("- " + Path.GetFileName(file));
        }

        var summaryPath = Path.Combine(proposalDirectory, config.Output.SummaryFile);
        if (File.Exists(summaryPath))
        {
            result.AddMessage($"Summary: {summaryPath}");
        }

        return result;
    }

    private static string ResolveProposalDirectory(string projectRoot, LlmOutputConfig output, DateTime nowUtc)
    {
        var week = ISOWeek.GetWeekOfYear(nowUtc);
        var relative = output.ProposalDirPattern
            .Replace("{iso_year}", nowUtc.Year.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{iso_week}", week.ToString("D2", CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace('/', Path.DirectorySeparatorChar);

        return Path.GetFullPath(Path.Combine(projectRoot, relative));
    }
}
