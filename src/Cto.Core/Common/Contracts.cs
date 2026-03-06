using System.Text.Json.Serialization;

namespace Cto.Core.Common;

public sealed class PlanDocument
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("goal")]
    public string Goal { get; set; } = string.Empty;

    [JsonPropertyName("success_criteria")]
    public List<string> SuccessCriteria { get; set; } = [];

    [JsonPropertyName("risks")]
    public List<RiskItem> Risks { get; set; } = [];

    [JsonPropertyName("work_breakdown")]
    public List<WorkPackage> WorkBreakdown { get; set; } = [];

    [JsonPropertyName("assumptions")]
    public List<string> Assumptions { get; set; } = [];

    [JsonPropertyName("dependencies")]
    public List<ExternalDependency> Dependencies { get; set; } = [];

    [JsonPropertyName("metadata")]
    public PlanMetadata? Metadata { get; set; }
}

public sealed class RiskItem
{
    [JsonPropertyName("risk")]
    public string Risk { get; set; } = string.Empty;

    [JsonPropertyName("mitigation")]
    public string Mitigation { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = string.Empty;

    [JsonPropertyName("ai_warned")]
    public bool? AiWarned { get; set; }
}

public sealed class WorkPackage
{
    [JsonPropertyName("epic_key")]
    public string EpicKey { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("stories")]
    public List<StoryItem> Stories { get; set; } = [];
}

public sealed class StoryItem
{
    [JsonPropertyName("story_key")]
    public string StoryKey { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("objective")]
    public string Objective { get; set; } = string.Empty;

    [JsonPropertyName("scope")]
    public StoryScope Scope { get; set; } = new();

    [JsonPropertyName("acceptance_criteria")]
    public List<string> AcceptanceCriteria { get; set; } = [];

    [JsonPropertyName("references")]
    public List<StoryReference> References { get; set; } = [];

    [JsonPropertyName("constraints")]
    public List<string> Constraints { get; set; } = [];

    [JsonPropertyName("definition_of_done")]
    public List<string> DefinitionOfDone { get; set; } = [];

    [JsonPropertyName("estimate")]
    public int Estimate { get; set; }

    [JsonPropertyName("priority")]
    public string? Priority { get; set; }

    [JsonPropertyName("labels")]
    public List<string> Labels { get; set; } = [];

    [JsonPropertyName("dependencies")]
    public List<string> Dependencies { get; set; } = [];
}

public sealed class StoryScope
{
    [JsonPropertyName("in_scope")]
    public List<string> InScope { get; set; } = [];

    [JsonPropertyName("out_of_scope")]
    public List<string> OutOfScope { get; set; } = [];
}

public sealed class StoryReference
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

public sealed class ExternalDependency
{
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("blocker")]
    public bool? Blocker { get; set; }
}

public sealed class PlanMetadata
{
    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; set; }

    [JsonPropertyName("approved_at")]
    public string? ApprovedAt { get; set; }

    [JsonPropertyName("approved_by")]
    public string? ApprovedBy { get; set; }

    [JsonPropertyName("commit_sha")]
    public string? CommitSha { get; set; }

    [JsonPropertyName("week_number")]
    public int? WeekNumber { get; set; }

    [JsonPropertyName("ai_proposal_followed")]
    public bool? AiProposalFollowed { get; set; }
}

public sealed class JiraConfig
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("jira")]
    public JiraInstance Jira { get; set; } = new();

    [JsonPropertyName("project")]
    public JiraProject Project { get; set; } = new();

    [JsonPropertyName("queries")]
    public Dictionary<string, string> Queries { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("snapshot")]
    public SnapshotConfig Snapshot { get; set; } = new();

    [JsonPropertyName("creation")]
    public CreationConfig Creation { get; set; } = new();

    [JsonPropertyName("validation")]
    public JiraValidationConfig Validation { get; set; } = new();
}

public sealed class JiraInstance
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("auth")]
    public JiraAuth Auth { get; set; } = new();
}

public sealed class JiraAuth
{
    [JsonPropertyName("email_env_var")]
    public string EmailEnvVar { get; set; } = "JIRA_EMAIL";

    [JsonPropertyName("api_token_env_var")]
    public string ApiTokenEnvVar { get; set; } = "JIRA_API_TOKEN";
}

public sealed class JiraProject
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = string.Empty;

    [JsonPropertyName("issue_types")]
    public IssueTypes IssueTypes { get; set; } = new();

    [JsonPropertyName("custom_fields")]
    public CustomFields CustomFields { get; set; } = new();

    [JsonPropertyName("defaults")]
    public JiraDefaults Defaults { get; set; } = new();
}

public sealed class IssueTypes
{
    [JsonPropertyName("epic")]
    public string Epic { get; set; } = string.Empty;

    [JsonPropertyName("story")]
    public string Story { get; set; } = string.Empty;

    [JsonPropertyName("task")]
    public string? Task { get; set; }

    [JsonPropertyName("bug")]
    public string? Bug { get; set; }
}

public sealed class CustomFields
{
    [JsonPropertyName("story_points")]
    public string StoryPoints { get; set; } = string.Empty;

    [JsonPropertyName("epic_link")]
    public string EpicLink { get; set; } = string.Empty;

    [JsonPropertyName("epic_name")]
    public string EpicName { get; set; } = string.Empty;

    [JsonPropertyName("acceptance_criteria")]
    public string? AcceptanceCriteria { get; set; }
}

public sealed class JiraDefaults
{
    [JsonPropertyName("assignee")]
    public string? Assignee { get; set; }

    [JsonPropertyName("reporter")]
    public string? Reporter { get; set; }

    [JsonPropertyName("priority")]
    public string? Priority { get; set; }

    [JsonPropertyName("labels")]
    public List<string> Labels { get; set; } = [];
}

public sealed class SnapshotConfig
{
    [JsonPropertyName("lookback_days")]
    public int LookbackDays { get; set; } = 7;

    [JsonPropertyName("max_results")]
    public int MaxResults { get; set; } = 100;

    [JsonPropertyName("fields")]
    public List<string> Fields { get; set; } = [];
}

public sealed class CreationConfig
{
    [JsonPropertyName("epics")]
    public EpicCreationConfig Epics { get; set; } = new();

    [JsonPropertyName("stories")]
    public StoryCreationConfig Stories { get; set; } = new();

    [JsonPropertyName("idempotency")]
    public IdempotencyConfig Idempotency { get; set; } = new();
}

public sealed class EpicCreationConfig
{
    [JsonPropertyName("name_format")]
    public string NameFormat { get; set; } = "{summary}";

    [JsonPropertyName("description_template")]
    public string? DescriptionTemplate { get; set; }
}

public sealed class StoryCreationConfig
{
    [JsonPropertyName("title_format")]
    public string TitleFormat { get; set; } = "{summary}";

    [JsonPropertyName("description_template")]
    public string? DescriptionTemplate { get; set; }

    [JsonPropertyName("auto_link_to_epic")]
    public bool AutoLinkToEpic { get; set; } = true;

    [JsonPropertyName("add_commit_label")]
    public bool AddCommitLabel { get; set; } = true;
}

public sealed class IdempotencyConfig
{
    [JsonPropertyName("issue_property_key")]
    public string IssuePropertyKey { get; set; } = "cto_engine.plan_commit_sha";

    [JsonPropertyName("check_issue_property")]
    public bool CheckIssueProperty { get; set; } = true;

    [JsonPropertyName("check_commit_sha_label")]
    public bool CheckCommitShaLabel { get; set; } = true;

    [JsonPropertyName("update_existing")]
    public bool UpdateExisting { get; set; } = true;

    [JsonPropertyName("update_fields")]
    public List<string> UpdateFields { get; set; } = [];

    [JsonPropertyName("preserve_fields")]
    public List<string> PreserveFields { get; set; } = [];
}

public sealed class JiraValidationConfig
{
    [JsonPropertyName("require_estimates")]
    public bool RequireEstimates { get; set; } = true;

    [JsonPropertyName("valid_estimates")]
    public List<int> ValidEstimates { get; set; } = [];

    [JsonPropertyName("max_story_points")]
    public int MaxStoryPoints { get; set; } = 13;

    [JsonPropertyName("max_stories_per_epic")]
    public int MaxStoriesPerEpic { get; set; } = 8;

    [JsonPropertyName("require_acceptance_criteria")]
    public bool RequireAcceptanceCriteria { get; set; } = true;

    [JsonPropertyName("min_acceptance_criteria")]
    public int MinAcceptanceCriteria { get; set; } = 1;
}
