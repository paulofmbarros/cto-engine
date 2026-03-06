using Cto.Core.Common;

namespace Cto.Core.Validation;

public enum ValidationTarget
{
    All,
    Plan,
    Context,
    WeeklyLog,
    JiraConfig,
}

public sealed class ValidationService
{
    private readonly PlanValidator _planValidator = new();
    private readonly ContextValidator _contextValidator = new();
    private readonly WeeklyLogValidator _weeklyLogValidator = new();
    private readonly JiraConfigValidator _jiraConfigValidator = new();

    public async Task<(ValidationResult Result, JiraConfig? JiraConfig)> ValidateAsync(
        ProjectPaths paths,
        ValidationTarget target,
        CancellationToken cancellationToken = default)
    {
        var result = new ValidationResult();
        JiraConfig? config = null;

        if (target is ValidationTarget.All or ValidationTarget.Plan)
        {
            result.Merge(await _planValidator.ValidateAsync(paths.PlanPath, cancellationToken));
        }

        if (target is ValidationTarget.All or ValidationTarget.Context)
        {
            result.Merge(_contextValidator.Validate(paths.ContextPath));
        }

        if (target is ValidationTarget.All or ValidationTarget.WeeklyLog)
        {
            result.Merge(_weeklyLogValidator.Validate(paths.WeeklyLogPath));
        }

        if (target is ValidationTarget.All or ValidationTarget.JiraConfig)
        {
            var configValidation = await _jiraConfigValidator.ValidateAsync(paths.JiraConfigPath, cancellationToken);
            config = configValidation.Config;
            result.Merge(configValidation.Result);
        }

        return (result, config);
    }
}
