using Cto.Core.Common;
using Cto.Core.Execution;
using Cto.Core.Init;
using Cto.Core.Planning;
using Cto.Core.Snapshot;
using Cto.Core.Validation;

namespace Cto.Core;

public sealed class CtoEngineApp
{
    private readonly string _engineRoot;
    private readonly SnapshotService _snapshotService = new();
    private readonly RealityCheckService _realityCheckService = new();
    private readonly ValidationService _validationService = new();
    private readonly ApprovalService _approvalService = new();
    private readonly ExecutionService _executionService = new();
    private readonly PlanProposalService _planProposalService = new();
    private readonly PlanSelectionService _planSelectionService = new();

    public CtoEngineApp(string engineRoot)
    {
        _engineRoot = engineRoot;
    }

    public OperationResult Init(string targetPath, bool force)
    {
        var service = new InitService(_engineRoot);
        return service.InitializeProjectPack(targetPath, force);
    }

    public Task<OperationResult> SnapshotAsync(string projectRoot, CancellationToken cancellationToken = default)
    {
        var paths = ProjectPaths.FromRoot(projectRoot);
        return _snapshotService.GenerateAsync(paths, cancellationToken);
    }

    public Task<OperationResult> RealityCheckAsync(string projectRoot, CancellationToken cancellationToken = default)
    {
        var paths = ProjectPaths.FromRoot(projectRoot);
        return _realityCheckService.GenerateAsync(paths, cancellationToken);
    }

    public async Task<OperationResult> ValidateAsync(string projectRoot, string target, CancellationToken cancellationToken = default)
    {
        var paths = ProjectPaths.FromRoot(projectRoot);
        if (!TryParseTarget(target, out var validationTarget, out var parseError))
        {
            return OperationResult.Fail(parseError!);
        }

        var validation = await _validationService.ValidateAsync(paths, validationTarget, cancellationToken);
        var operation = validation.Result.IsValid
            ? OperationResult.Ok("Validation passed.")
            : OperationResult.Fail("Validation failed.");

        foreach (var issue in validation.Result.Issues)
        {
            operation.AddMessage(issue.ToString());
        }

        return operation;
    }

    public async Task<OperationResult> PlanInteractiveAsync(string projectRoot, bool interactive, CancellationToken cancellationToken = default)
    {
        if (!interactive)
        {
            return OperationResult.Fail("Use --interactive for this command in v1.");
        }

        var paths = ProjectPaths.FromRoot(projectRoot);
        var contextValidation = await _validationService.ValidateAsync(paths, ValidationTarget.Context, cancellationToken);
        var weeklyValidation = await _validationService.ValidateAsync(paths, ValidationTarget.WeeklyLog, cancellationToken);

        var gateResult = new ValidationResult();
        gateResult.Merge(contextValidation.Result);
        gateResult.Merge(weeklyValidation.Result);

        if (!gateResult.IsValid)
        {
            var messages = gateResult.Issues.Select(i => i.ToString()).ToList();
            messages.Insert(0, "Planning blocked: context/weeklylog validation failed.");
            return OperationResult.Fail(messages);
        }

        var service = new PlanBundleService(_engineRoot);
        return await service.GenerateAsync(paths, cancellationToken);
    }

    public async Task<OperationResult> PlanWithLlmAsync(
        string projectRoot,
        PlanLlmOptions options,
        CancellationToken cancellationToken = default)
    {
        var paths = ProjectPaths.FromRoot(projectRoot);

        var contextValidation = await _validationService.ValidateAsync(paths, ValidationTarget.Context, cancellationToken);
        var weeklyValidation = await _validationService.ValidateAsync(paths, ValidationTarget.WeeklyLog, cancellationToken);
        var gateResult = new ValidationResult();
        gateResult.Merge(contextValidation.Result);
        gateResult.Merge(weeklyValidation.Result);

        if (!gateResult.IsValid)
        {
            var messages = gateResult.Issues.Select(i => i.ToString()).ToList();
            messages.Insert(0, "LLM planning blocked: context/weeklylog validation failed.");
            return OperationResult.Fail(messages);
        }

        return await _planProposalService.GenerateAsync(paths, options, cancellationToken);
    }

    public Task<OperationResult> SelectPlanCandidateAsync(
        string projectRoot,
        int candidateIndex,
        CancellationToken cancellationToken = default)
    {
        var paths = ProjectPaths.FromRoot(projectRoot);
        return _planSelectionService.SelectAsync(paths, candidateIndex, cancellationToken);
    }

    public Task<OperationResult> ListPlanCandidatesAsync(
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        var paths = ProjectPaths.FromRoot(projectRoot);
        return _planSelectionService.ListAsync(paths, cancellationToken);
    }

    public Task<OperationResult> ApproveAsync(string projectRoot, CancellationToken cancellationToken = default)
    {
        var paths = ProjectPaths.FromRoot(projectRoot);
        return _approvalService.ApproveAsync(paths, cancellationToken);
    }

    public Task<OperationResult> ExecuteAsync(string projectRoot, bool dryRun, CancellationToken cancellationToken = default)
    {
        var paths = ProjectPaths.FromRoot(projectRoot);
        return _executionService.ExecuteAsync(paths, dryRun, cancellationToken);
    }

    private static bool TryParseTarget(string target, out ValidationTarget validationTarget, out string? error)
    {
        error = null;
        switch (target.Trim().ToLowerInvariant())
        {
            case "all":
                validationTarget = ValidationTarget.All;
                return true;
            case "plan":
                validationTarget = ValidationTarget.Plan;
                return true;
            case "context":
                validationTarget = ValidationTarget.Context;
                return true;
            case "weeklylog":
                validationTarget = ValidationTarget.WeeklyLog;
                return true;
            case "jira-config":
            case "jiraconfig":
                validationTarget = ValidationTarget.JiraConfig;
                return true;
            case "llm-config":
            case "llmconfig":
                validationTarget = ValidationTarget.LlmConfig;
                return true;
            default:
                validationTarget = ValidationTarget.All;
                error = "Invalid --target. Expected one of: all, plan, context, weeklylog, jira-config, llm-config.";
                return false;
        }
    }
}
