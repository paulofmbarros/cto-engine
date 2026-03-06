using System.Globalization;
using Cto.Core.Common;
using Cto.Core.Git;
using Cto.Core.Validation;

namespace Cto.Core.Execution;

public sealed class ApprovalService
{
    private readonly ValidationService _validationService = new();
    private readonly GitService _gitService = new();

    public async Task<OperationResult> ApproveAsync(ProjectPaths paths, CancellationToken cancellationToken = default)
    {
        var validation = await _validationService.ValidateAsync(paths, ValidationTarget.All, cancellationToken);
        if (!validation.Result.IsValid)
        {
            var messages = validation.Result.Issues.Select(i => i.ToString()).ToList();
            messages.Insert(0, "Approval blocked: validation failed.");
            return OperationResult.Fail(messages);
        }

        if (!await _gitService.IsGitRepositoryAsync(paths.Root, cancellationToken))
        {
            return OperationResult.Fail("Approval blocked: project root is not a git repository.");
        }

        var status = await _gitService.GetChangedPathsAsync(paths.Root, cancellationToken);
        if (!status.Success)
        {
            return OperationResult.Fail($"Approval blocked: cannot read git status: {status.Error}");
        }

        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "plan.yaml",
            ".cto-engine/challenge-log.yaml",
        };

        if (status.ChangedPaths.Count == 0)
        {
            return OperationResult.Fail("Approval blocked: no working tree changes found.");
        }

        var disallowed = status.ChangedPaths.Where(path => !allowed.Contains(path)).ToList();
        if (disallowed.Count > 0)
        {
            var message = "Approval blocked: working tree has changes outside plan approval scope: " + string.Join(", ", disallowed);
            return OperationResult.Fail(message);
        }

        var now = DateTime.UtcNow;
        var week = ISOWeek.GetWeekOfYear(now);
        var commitMessage = $"cto-engine: approve plan {now.Year}-W{week:D2}";

        var filesToCommit = status.ChangedPaths
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        var commit = await _gitService.CommitAsync(paths.Root, filesToCommit, commitMessage, cancellationToken);
        if (!commit.Success)
        {
            return OperationResult.Fail($"Approval failed: {commit.Error}");
        }

        return OperationResult.Ok(
            "Plan approved.",
            $"Commit message: {commitMessage}",
            $"Commit SHA: {commit.Sha}");
    }
}
