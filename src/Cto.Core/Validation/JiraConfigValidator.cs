using Cto.Core.Common;

namespace Cto.Core.Validation;

public sealed class JiraConfigValidator
{
    public async Task<(ValidationResult Result, JiraConfig? Config)> ValidateAsync(string jiraConfigPath, CancellationToken cancellationToken = default)
    {
        var result = new ValidationResult();
        if (!File.Exists(jiraConfigPath))
        {
            result.AddError("JIRA_CONFIG_MISSING", "jira-config.yaml is missing.", jiraConfigPath);
            return (result, null);
        }

        var (config, error) = await YamlBridge.LoadAsAsync<JiraConfig>(jiraConfigPath, cancellationToken);
        if (error is not null)
        {
            result.AddError("JIRA_CONFIG_PARSE", error, jiraConfigPath);
            return (result, null);
        }

        if (config is null)
        {
            result.AddError("JIRA_CONFIG_EMPTY", "jira-config.yaml is empty or invalid.", jiraConfigPath);
            return (result, null);
        }

        if (string.IsNullOrWhiteSpace(config.Jira.Url))
        {
            result.AddError("JIRA_URL_MISSING", "jira.url is required.", jiraConfigPath);
        }

        if (string.IsNullOrWhiteSpace(config.Jira.Auth.EmailEnvVar) || string.IsNullOrWhiteSpace(config.Jira.Auth.ApiTokenEnvVar))
        {
            result.AddError("JIRA_AUTH_ENV", "jira.auth.email_env_var and jira.auth.api_token_env_var are required.", jiraConfigPath);
        }

        if (string.IsNullOrWhiteSpace(config.Project.Key))
        {
            result.AddError("JIRA_PROJECT_KEY", "project.key is required.", jiraConfigPath);
        }

        var mode = config.Project.Mode?.Trim().ToLowerInvariant();
        if (mode is not ("company_managed" or "team_managed"))
        {
            result.AddError("JIRA_MODE", "project.mode must be 'company_managed' or 'team_managed'.", jiraConfigPath);
        }

        if (string.IsNullOrWhiteSpace(config.Project.IssueTypes.Epic) || string.IsNullOrWhiteSpace(config.Project.IssueTypes.Story))
        {
            result.AddError("JIRA_ISSUE_TYPES", "project.issue_types.epic and project.issue_types.story are required.", jiraConfigPath);
        }

        if (string.IsNullOrWhiteSpace(config.Project.CustomFields.EpicLink))
        {
            result.AddError("JIRA_EPIC_LINK", "project.custom_fields.epic_link is required for ticket linking.", jiraConfigPath);
        }

        if (string.IsNullOrWhiteSpace(config.Project.CustomFields.StoryPoints))
        {
            result.AddError("JIRA_STORY_POINTS", "project.custom_fields.story_points is required.", jiraConfigPath);
        }

        if (config.Queries.Count == 0)
        {
            result.AddError("JIRA_QUERIES", "At least one JQL query is required under queries.", jiraConfigPath);
        }

        if (config.Creation.Idempotency.CheckIssueProperty && string.IsNullOrWhiteSpace(config.Creation.Idempotency.IssuePropertyKey))
        {
            result.AddError("JIRA_IDEMPOTENCY_PROPERTY", "creation.idempotency.issue_property_key is required when check_issue_property is true.", jiraConfigPath);
        }

        var validEstimates = config.Validation.ValidEstimates;
        var requiredEstimates = new[] { 1, 2, 3, 5, 8, 13 };
        if (requiredEstimates.Any(value => !validEstimates.Contains(value)))
        {
            result.AddError("JIRA_VALID_ESTIMATES", "validation.valid_estimates must include Fibonacci values: 1,2,3,5,8,13.", jiraConfigPath);
        }

        return (result, config);
    }
}
