using System.Text.Json;
using System.Text.Json.Nodes;
using Cto.Core.Common;
using Cto.Core.Jira;
using Cto.Core.Validation;

namespace Cto.Core.Snapshot;

public sealed class SnapshotService
{
    private readonly JiraConfigValidator _jiraConfigValidator = new();

    public async Task<OperationResult> GenerateAsync(ProjectPaths paths, CancellationToken cancellationToken = default)
    {
        var result = OperationResult.Ok();

        Directory.CreateDirectory(paths.CtoEngineDirectory);

        var configValidation = await _jiraConfigValidator.ValidateAsync(paths.JiraConfigPath, cancellationToken);
        if (!configValidation.Result.IsValid || configValidation.Config is null)
        {
            var messages = configValidation.Result.Issues.Select(i => i.ToString()).ToList();
            messages.Insert(0, "Snapshot aborted because jira-config validation failed.");
            return OperationResult.Fail(messages);
        }

        var config = configValidation.Config;

        try
        {
            using var jira = new JiraClient(config);
            var queryPayload = new JsonObject();

            foreach (var query in config.Queries.OrderBy(q => q.Key, StringComparer.Ordinal))
            {
                var projectKeyLiteral = EscapeJqlLiteral(config.Project.Key);
                var jql = query.Value.Replace("{project_key}", $"\"{projectKeyLiteral}\"", StringComparison.OrdinalIgnoreCase);
                var issues = await jira.SearchAsync(jql, config.Snapshot.MaxResults, config.Snapshot.Fields, cancellationToken);

                queryPayload[query.Key] = new JsonObject
                {
                    ["jql"] = jql,
                    ["count"] = issues.Count,
                    ["issues"] = new JsonArray(issues
                        .OrderBy(i => i.Key, StringComparer.Ordinal)
                        .Select(issue => new JsonObject
                        {
                            ["key"] = issue.Key,
                            ["summary"] = issue.Summary,
                            ["status"] = issue.Status,
                            ["assignee"] = issue.Assignee,
                            ["updated"] = issue.Updated,
                            ["labels"] = new JsonArray(issue.Labels.Select(label => JsonValue.Create(label)).ToArray()),
                        })
                        .ToArray()),
                };
            }

            var snapshot = new JsonObject
            {
                ["generated_at"] = DateTimeOffset.UtcNow.ToString("O"),
                ["project_key"] = config.Project.Key,
                ["project_name"] = config.Project.Name,
                ["queries"] = queryPayload,
            };

            await File.WriteAllTextAsync(paths.SnapshotPath, snapshot.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
            }), cancellationToken);

            result.AddMessage($"Snapshot written to {paths.SnapshotPath}");
            return result;
        }
        catch (Exception ex)
        {
            if (File.Exists(paths.SnapshotPath))
            {
                result.AddMessage($"Warning: Jira unreachable. Keeping previous snapshot at {paths.SnapshotPath}");
                result.AddMessage($"Cause: {ex.Message}");
                return result;
            }

            return OperationResult.Fail(
                "Snapshot failed and no previous snapshot exists.",
                ex.Message);
        }
    }

    private static string EscapeJqlLiteral(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}
