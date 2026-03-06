using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Cto.Core.Common;
using Cto.Core.Git;
using Cto.Core.Jira;
using Cto.Core.Snapshot;
using Cto.Core.Validation;

namespace Cto.Core.Execution;

public sealed class ExecutionService
{
    private readonly ValidationService _validationService = new();
    private readonly GitService _gitService = new();
    private readonly SnapshotService _snapshotService = new();

    public async Task<OperationResult> ExecuteAsync(ProjectPaths paths, bool dryRun, CancellationToken cancellationToken = default)
    {
        var validation = await _validationService.ValidateAsync(paths, ValidationTarget.All, cancellationToken);
        if (!validation.Result.IsValid || validation.JiraConfig is null)
        {
            var messages = validation.Result.Issues.Select(i => i.ToString()).ToList();
            messages.Insert(0, "Execute blocked: validation failed.");
            return OperationResult.Fail(messages);
        }

        var jiraConfig = validation.JiraConfig;
        var mode = jiraConfig.Project.Mode.Trim().ToLowerInvariant();
        var isCompanyManaged = string.Equals(mode, "company_managed", StringComparison.Ordinal);
        var isTeamManaged = string.Equals(mode, "team_managed", StringComparison.Ordinal);
        if (!isCompanyManaged && !isTeamManaged)
        {
            return OperationResult.Fail("Execute blocked: project.mode must be company_managed or team_managed.");
        }

        if (string.IsNullOrWhiteSpace(jiraConfig.Project.CustomFields.EpicLink))
        {
            return OperationResult.Fail("Execute blocked: jira-config project.custom_fields.epic_link is required.");
        }

        if (string.IsNullOrWhiteSpace(jiraConfig.Project.CustomFields.StoryPoints))
        {
            return OperationResult.Fail("Execute blocked: jira-config project.custom_fields.story_points is required.");
        }

        var headCommit = await _gitService.GetHeadCommitMessageAsync(paths.Root, cancellationToken);
        if (!headCommit.Success)
        {
            return OperationResult.Fail($"Execute blocked: unable to read HEAD commit: {headCommit.Error}");
        }

        if (!headCommit.Message.StartsWith("cto-engine: approve plan ", StringComparison.Ordinal))
        {
            return OperationResult.Fail("Execute blocked: HEAD is not an approval commit (expected prefix 'cto-engine: approve plan ').");
        }

        var headSha = await _gitService.GetHeadCommitShaAsync(paths.Root, cancellationToken);
        if (!headSha.Success)
        {
            return OperationResult.Fail($"Execute blocked: unable to resolve HEAD SHA: {headSha.Error}");
        }

        var commitSha = headSha.Sha;
        var commitLabel = "cto-commit-" + commitSha[..12];

        var (plan, planError) = await YamlBridge.LoadAsAsync<PlanDocument>(paths.PlanPath, cancellationToken);
        if (planError is not null || plan is null)
        {
            return OperationResult.Fail(planError ?? "Failed to parse plan.yaml.");
        }

        var report = new List<string>();
        var createdIssues = new List<string>();
        var updatedIssues = new List<string>();

        Directory.CreateDirectory(paths.LogsDirectory);

        if (dryRun)
        {
            foreach (var workPackage in plan.WorkBreakdown)
            {
                report.Add($"[DRY RUN] EPIC create/update: {workPackage.Summary}");
                foreach (var story in workPackage.Stories)
                {
                    report.Add($"[DRY RUN] STORY create/update: {story.Summary}");
                }
            }

            await WriteExecutionLogAsync(paths, report, cancellationToken);
            return OperationResult.Ok(
                "Dry-run completed.",
                $"Planned operations: {report.Count}",
                $"Execution log: {paths.LogsDirectory}");
        }

        using var jira = new JiraClient(jiraConfig);

        var existing = new List<JiraIssue>();
        if (jiraConfig.Creation.Idempotency.CheckIssueProperty)
        {
            var byProperty = await jira.SearchByIssuePropertyAsync(
                jiraConfig.Project.Key,
                jiraConfig.Creation.Idempotency.IssuePropertyKey,
                commitSha,
                cancellationToken);

            existing.AddRange(byProperty);
        }

        if (existing.Count == 0 && jiraConfig.Creation.Idempotency.CheckCommitShaLabel)
        {
            var byLabel = await jira.SearchByCommitLabelAsync(jiraConfig.Project.Key, commitLabel, cancellationToken);
            existing.AddRange(byLabel);
        }

        var existingBySummary = existing
            .GroupBy(issue => issue.Summary.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var workPackage in plan.WorkBreakdown)
        {
            var epicKey = workPackage.EpicKey;
            if (string.IsNullOrWhiteSpace(epicKey) && existingBySummary.TryGetValue(workPackage.Summary.Trim(), out var existingEpic))
            {
                epicKey = existingEpic.Key;
            }

            if (string.IsNullOrWhiteSpace(epicKey))
            {
                var epicFields = new JsonObject
                {
                    ["project"] = new JsonObject { ["key"] = jiraConfig.Project.Key },
                    ["issuetype"] = new JsonObject { ["id"] = jiraConfig.Project.IssueTypes.Epic },
                    ["summary"] = workPackage.Summary,
                    ["description"] = workPackage.Description ?? string.Empty,
                    ["labels"] = BuildLabels(jiraConfig.Project.Defaults.Labels, [], commitLabel),
                };

                if (isCompanyManaged && !string.IsNullOrWhiteSpace(jiraConfig.Project.CustomFields.EpicName))
                {
                    epicFields[jiraConfig.Project.CustomFields.EpicName] = workPackage.Summary;
                }

                epicKey = await jira.CreateIssueAsync(epicFields, cancellationToken);
                await jira.SetIssuePropertyAsync(epicKey, jiraConfig.Creation.Idempotency.IssuePropertyKey, commitSha, cancellationToken);
                report.Add($"Created EPIC {epicKey}: {workPackage.Summary}");
                createdIssues.Add(epicKey);
            }

            foreach (var story in workPackage.Stories)
            {
                if (!string.IsNullOrWhiteSpace(story.StoryKey))
                {
                    updatedIssues.Add(story.StoryKey);
                    report.Add($"Skipped create for explicit STORY key {story.StoryKey}: {story.Summary}");
                    continue;
                }

                var description = BuildStoryDescription(story, epicKey);
                var labels = BuildLabels(jiraConfig.Project.Defaults.Labels, story.Labels, commitLabel);
                var priorityName = MapPriority(story.Priority, jiraConfig.Project.Defaults.Priority);

                if (existingBySummary.TryGetValue(story.Summary.Trim(), out var existingStory) && jiraConfig.Creation.Idempotency.UpdateExisting)
                {
                    var fields = new JsonObject();
                    var updateFields = jiraConfig.Creation.Idempotency.UpdateFields;

                    if (updateFields.Contains("description", StringComparer.OrdinalIgnoreCase))
                    {
                        fields["description"] = description;
                    }

                    if (updateFields.Contains("labels", StringComparer.OrdinalIgnoreCase))
                    {
                        fields["labels"] = labels;
                    }

                    if (updateFields.Contains("priority", StringComparer.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(priorityName))
                    {
                        fields["priority"] = new JsonObject { ["name"] = priorityName };
                    }

                    if (fields.Count > 0)
                    {
                        await jira.UpdateIssueAsync(existingStory.Key, fields, cancellationToken);
                    }

                    await jira.SetIssuePropertyAsync(existingStory.Key, jiraConfig.Creation.Idempotency.IssuePropertyKey, commitSha, cancellationToken);
                    report.Add($"Updated STORY {existingStory.Key}: {story.Summary}");
                    updatedIssues.Add(existingStory.Key);
                    continue;
                }

                var storyFields = new JsonObject
                {
                    ["project"] = new JsonObject { ["key"] = jiraConfig.Project.Key },
                    ["issuetype"] = new JsonObject { ["id"] = jiraConfig.Project.IssueTypes.Story },
                    ["summary"] = story.Summary,
                    ["description"] = description,
                    [jiraConfig.Project.CustomFields.StoryPoints] = story.Estimate,
                    ["labels"] = labels,
                };

                if (isTeamManaged)
                {
                    // Team-managed projects link story->epic via "parent".
                    storyFields["parent"] = new JsonObject { ["key"] = epicKey };
                }
                else
                {
                    storyFields[jiraConfig.Project.CustomFields.EpicLink] = epicKey;
                }

                if (!string.IsNullOrWhiteSpace(priorityName))
                {
                    storyFields["priority"] = new JsonObject { ["name"] = priorityName };
                }

                var newStoryKey = await jira.CreateIssueAsync(storyFields, cancellationToken);
                await jira.SetIssuePropertyAsync(newStoryKey, jiraConfig.Creation.Idempotency.IssuePropertyKey, commitSha, cancellationToken);

                report.Add($"Created STORY {newStoryKey}: {story.Summary}");
                createdIssues.Add(newStoryKey);
            }
        }

        await WriteExecutionLogAsync(paths, report, cancellationToken);

        var snapshotResult = await _snapshotService.GenerateAsync(paths, cancellationToken);
        var finalResult = OperationResult.Ok(
            $"Execution complete. Created: {createdIssues.Count}, Updated: {updatedIssues.Count}",
            $"Execution log written to {paths.LogsDirectory}");

        foreach (var message in snapshotResult.Messages)
        {
            finalResult.AddMessage(message);
        }

        if (!snapshotResult.Success)
        {
            finalResult.AddFailure("Execution succeeded but snapshot refresh failed.");
        }

        return finalResult;
    }

    private static async Task WriteExecutionLogAsync(ProjectPaths paths, IReadOnlyCollection<string> report, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(paths.LogsDirectory);
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        var logPath = Path.Combine(paths.LogsDirectory, $"execute-{timestamp}.log");
        await File.WriteAllLinesAsync(logPath, report, cancellationToken);
    }

    private static JsonArray BuildLabels(IEnumerable<string> defaults, IEnumerable<string> storyLabels, string commitLabel)
    {
        var labels = defaults
            .Concat(storyLabels)
            .Append(commitLabel)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(label => label, StringComparer.OrdinalIgnoreCase)
            .Select(label => JsonValue.Create(label))
            .ToArray();

        return new JsonArray(labels);
    }

    private static string BuildStoryDescription(StoryItem story, string epicKey)
    {
        var builder = new StringBuilder();
        builder.AppendLine("h2. Objective");
        builder.AppendLine(story.Objective);
        builder.AppendLine();
        builder.AppendLine("h2. Scope");
        builder.AppendLine("h3. In scope");
        foreach (var item in story.Scope.InScope)
        {
            builder.AppendLine($"* {item}");
        }

        builder.AppendLine();
        builder.AppendLine("h3. Out of scope");
        foreach (var item in story.Scope.OutOfScope)
        {
            builder.AppendLine($"* {item}");
        }

        builder.AppendLine();
        builder.AppendLine("h2. Acceptance Criteria");
        foreach (var criterion in story.AcceptanceCriteria)
        {
            builder.AppendLine($"# {criterion}");
        }

        if (story.References.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("h2. References");
            foreach (var reference in story.References)
            {
                builder.AppendLine($"* [{reference.Title}|{reference.Url}]");
            }
        }

        if (story.Constraints.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("h2. Constraints / Rules");
            foreach (var constraint in story.Constraints)
            {
                builder.AppendLine($"* {constraint}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("h2. Definition of Done");
        foreach (var done in story.DefinitionOfDone)
        {
            builder.AppendLine($"* {done}");
        }

        builder.AppendLine();
        builder.AppendLine("---");
        builder.AppendLine($"_Epic: {epicKey}_");
        builder.AppendLine($"_Estimate: {story.Estimate} points_");

        return builder.ToString().TrimEnd();
    }

    private static string MapPriority(string? storyPriority, string? defaultPriority)
    {
        var source = string.IsNullOrWhiteSpace(storyPriority) ? defaultPriority : storyPriority;
        if (string.IsNullOrWhiteSpace(source))
        {
            return string.Empty;
        }

        return source.Trim().ToLowerInvariant() switch
        {
            "lowest" => "Lowest",
            "low" => "Low",
            "medium" => "Medium",
            "high" => "High",
            "highest" => "Highest",
            _ => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(source.Trim().ToLowerInvariant()),
        };
    }
}
