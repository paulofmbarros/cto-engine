using System.Text;
using System.Text.Json.Nodes;
using Cto.Core.Common;

namespace Cto.Core.Planning;

public sealed class RealityCheckService
{
    public async Task<OperationResult> GenerateAsync(ProjectPaths paths, CancellationToken cancellationToken = default)
    {
        var result = OperationResult.Ok();

        if (!File.Exists(paths.PlanPath))
        {
            return OperationResult.Fail($"Missing plan file: {paths.PlanPath}");
        }

        if (!File.Exists(paths.SnapshotPath))
        {
            return OperationResult.Fail($"Missing snapshot file: {paths.SnapshotPath}. Run 'cto-engine snapshot' first.");
        }

        var (plan, planError) = await YamlBridge.LoadAsAsync<PlanDocument>(paths.PlanPath, cancellationToken);
        if (planError is not null || plan is null)
        {
            return OperationResult.Fail(planError ?? "Failed to parse plan.yaml.");
        }

        var snapshotJson = await File.ReadAllTextAsync(paths.SnapshotPath, cancellationToken);
        var snapshotNode = JsonNode.Parse(snapshotJson) as JsonObject;
        if (snapshotNode is null)
        {
            return OperationResult.Fail("snapshot.json is invalid JSON.");
        }

        var issues = ExtractIssues(snapshotNode)
            .GroupBy(issue => issue.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        var completed = new List<string>();
        var inProgress = new List<string>();
        var blocked = new List<string>();
        var notStarted = new List<string>();

        foreach (var story in plan.WorkBreakdown.SelectMany(epic => epic.Stories))
        {
            var matchedIssue = MatchIssue(story, issues);
            if (matchedIssue is null)
            {
                notStarted.Add($"{story.Summary} (not found in snapshot)");
                continue;
            }

            var status = matchedIssue.Status.ToLowerInvariant();
            var line = $"{matchedIssue.Key}: {story.Summary} [{matchedIssue.Status}]";

            if (status.Contains("block", StringComparison.Ordinal))
            {
                blocked.Add(line);
            }
            else if (status.Contains("done", StringComparison.Ordinal) || status.Contains("closed", StringComparison.Ordinal) || status.Contains("resolved", StringComparison.Ordinal))
            {
                completed.Add(line);
            }
            else if (status.Contains("progress", StringComparison.Ordinal) || status.Contains("review", StringComparison.Ordinal))
            {
                inProgress.Add(line);
            }
            else
            {
                notStarted.Add(line);
            }
        }

        var plannedCount = plan.WorkBreakdown.Sum(epic => epic.Stories.Count);
        var completedPercent = plannedCount == 0 ? 0 : (int)Math.Round(completed.Count * 100.0 / plannedCount);

        var driftNotes = new List<string>();
        if (completedPercent < 50)
        {
            driftNotes.Add($"Velocity drift detected: only {completedPercent}% of planned stories are complete.");
        }

        if (blocked.Count > 0)
        {
            driftNotes.Add($"Blocked stories present: {blocked.Count}. Re-assess dependencies and mitigation sequencing.");
        }

        if (driftNotes.Count == 0)
        {
            driftNotes.Add("Execution is broadly aligned with plan intent.");
        }

        var markdown = BuildRealityCheckMarkdown(completed, inProgress, blocked, notStarted, driftNotes, plannedCount, completedPercent);

        Directory.CreateDirectory(paths.CtoEngineDirectory);
        await File.WriteAllTextAsync(paths.RealityCheckPath, markdown, cancellationToken);

        result.AddMessage($"Reality check written to {paths.RealityCheckPath}");
        return result;
    }

    private static string BuildRealityCheckMarkdown(
        List<string> completed,
        List<string> inProgress,
        List<string> blocked,
        List<string> notStarted,
        List<string> driftNotes,
        int plannedCount,
        int completedPercent)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Reality Check");
        builder.AppendLine();
        builder.AppendLine($"> Generated: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        builder.AppendLine();
        builder.AppendLine("## Summary");
        builder.AppendLine($"- Planned stories: {plannedCount}");
        builder.AppendLine($"- Completed: {completed.Count} ({completedPercent}%)");
        builder.AppendLine($"- In progress: {inProgress.Count}");
        builder.AppendLine($"- Blocked: {blocked.Count}");
        builder.AppendLine($"- Not started: {notStarted.Count}");
        builder.AppendLine();

        WriteSection(builder, "Completed", completed);
        WriteSection(builder, "In Progress", inProgress);
        WriteSection(builder, "Blocked", blocked);
        WriteSection(builder, "Not Started", notStarted);
        WriteSection(builder, "Drift Notes", driftNotes);

        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    private static void WriteSection(StringBuilder builder, string title, IReadOnlyCollection<string> lines)
    {
        builder.AppendLine($"## {title}");
        if (lines.Count == 0)
        {
            builder.AppendLine("- None");
        }
        else
        {
            foreach (var line in lines)
            {
                builder.AppendLine($"- {line}");
            }
        }

        builder.AppendLine();
    }

    private static JiraSnapshotIssue? MatchIssue(StoryItem story, IReadOnlyList<JiraSnapshotIssue> issues)
    {
        if (!string.IsNullOrWhiteSpace(story.StoryKey))
        {
            var byKey = issues.FirstOrDefault(issue => string.Equals(issue.Key, story.StoryKey, StringComparison.OrdinalIgnoreCase));
            if (byKey is not null)
            {
                return byKey;
            }
        }

        return issues.FirstOrDefault(issue => string.Equals(issue.Summary, story.Summary, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<JiraSnapshotIssue> ExtractIssues(JsonObject snapshot)
    {
        var issues = new List<JiraSnapshotIssue>();
        if (snapshot["queries"] is not JsonObject queries)
        {
            return issues;
        }

        foreach (var query in queries)
        {
            if (query.Value is not JsonObject payload || payload["issues"] is not JsonArray issuesArray)
            {
                continue;
            }

            foreach (var issueNode in issuesArray)
            {
                if (issueNode is not JsonObject issue)
                {
                    continue;
                }

                issues.Add(new JiraSnapshotIssue
                {
                    Key = issue["key"]?.GetValue<string?>() ?? string.Empty,
                    Summary = issue["summary"]?.GetValue<string?>() ?? string.Empty,
                    Status = issue["status"]?.GetValue<string?>() ?? string.Empty,
                });
            }
        }

        return issues;
    }

    private sealed class JiraSnapshotIssue
    {
        public string Key { get; init; } = string.Empty;
        public string Summary { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
    }
}
