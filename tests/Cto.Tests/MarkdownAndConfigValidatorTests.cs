using Cto.Core.Validation;

namespace Cto.Tests;

public sealed class MarkdownAndConfigValidatorTests
{
    [Fact]
    public void ContextValidationFailsWhenRequiredSectionsMissing()
    {
        var path = WriteTemp("context", "# Context\n\n## What exists? (Production State)\n- API\n");

        var validator = new ContextValidator();
        var result = validator.Validate(path);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "CONTEXT_HEADING_MISSING");
    }

    [Fact]
    public void WeeklyLogValidationFailsWithoutStructuredEntriesOrNone()
    {
        var path = WriteTemp("weeklylog", """
# Weekly Log

## What surprised you this week?
Surprising.

## What assumption broke?
No assumptions broke this week

## What did users actually do?
Behavior matched expectations

## What blocked you?
We had delays.

## What did you learn?
No significant learnings this week
""");

        var validator = new WeeklyLogValidator();
        var result = validator.Validate(path);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code is "WEEKLYLOG_SECTION_STRUCTURE" or "WEEKLYLOG_BLOCKER_TIME");
    }

    [Fact]
    public async Task JiraConfigAcceptsTeamManagedMode()
    {
        var path = WriteTemp("jira-config", """
version: "1.0"
jira:
  url: "https://example.atlassian.net"
  auth:
    email_env_var: "JIRA_EMAIL"
    api_token_env_var: "JIRA_API_TOKEN"
project:
  key: "ABC"
  name: "Example"
  mode: "team_managed"
  issue_types:
    epic: "10000"
    story: "10001"
  custom_fields:
    story_points: "customfield_10016"
    epic_link: "customfield_10014"
    epic_name: "customfield_10011"
  defaults:
    labels: ["cto-engine"]
queries:
  active_work: "project = ABC"
snapshot:
  lookback_days: 7
  max_results: 100
  fields: ["summary"]
creation:
  epics: {}
  stories: {}
  idempotency:
    issue_property_key: "cto_engine.plan_commit_sha"
    check_issue_property: true
    check_commit_sha_label: true
    update_existing: true
    update_fields: ["description"]
    preserve_fields: ["status"]
validation:
  require_estimates: true
  valid_estimates: [1,2,3,5,8,13]
  max_story_points: 13
  max_stories_per_epic: 8
  require_acceptance_criteria: true
  min_acceptance_criteria: 1
""");

        var validator = new JiraConfigValidator();
        var (result, _) = await validator.ValidateAsync(path);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task JiraConfigRejectsUnknownProjectMode()
    {
        var path = WriteTemp("jira-config", """
version: "1.0"
jira:
  url: "https://example.atlassian.net"
  auth:
    email_env_var: "JIRA_EMAIL"
    api_token_env_var: "JIRA_API_TOKEN"
project:
  key: "ABC"
  name: "Example"
  mode: "invalid_mode"
  issue_types:
    epic: "10000"
    story: "10001"
  custom_fields:
    story_points: "customfield_10016"
    epic_link: "customfield_10014"
    epic_name: "customfield_10011"
  defaults:
    labels: ["cto-engine"]
queries:
  active_work: "project = ABC"
snapshot:
  lookback_days: 7
  max_results: 100
  fields: ["summary"]
creation:
  epics: {}
  stories: {}
  idempotency:
    issue_property_key: "cto_engine.plan_commit_sha"
    check_issue_property: true
    check_commit_sha_label: true
    update_existing: true
    update_fields: ["description"]
    preserve_fields: ["status"]
validation:
  require_estimates: true
  valid_estimates: [1,2,3,5,8,13]
  max_story_points: 13
  max_stories_per_epic: 8
  require_acceptance_criteria: true
  min_acceptance_criteria: 1
""");

        var validator = new JiraConfigValidator();
        var (result, _) = await validator.ValidateAsync(path);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "JIRA_MODE");
    }

    private static string WriteTemp(string name, string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{name}-{Guid.NewGuid():N}.md");
        File.WriteAllText(path, content);
        return path;
    }
}
