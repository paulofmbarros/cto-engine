using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cto.Core.Common;

namespace Cto.Core.Jira;

public sealed class JiraClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _disposeClient;
    private readonly JiraConfig _config;

    public JiraClient(JiraConfig config, HttpClient? httpClient = null)
    {
        _config = config;

        if (httpClient is not null)
        {
            _httpClient = httpClient;
            _disposeClient = false;
            return;
        }

        _httpClient = BuildAuthenticatedClient(config);
        _disposeClient = true;
    }

    public async Task<IReadOnlyList<JiraIssue>> SearchAsync(
        string jql,
        int maxResults,
        IReadOnlyList<string>? fields = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new JsonObject
        {
            ["jql"] = jql,
            ["maxResults"] = maxResults,
        };

        if (fields is { Count: > 0 })
        {
            payload["fields"] = new JsonArray(fields.Select(field => JsonValue.Create(field)).ToArray());
        }

        var response = await SendAsync(HttpMethod.Post, "/rest/api/3/search/jql", payload, cancellationToken);
        var root = JsonNode.Parse(response) as JsonObject;
        var issues = root?["issues"] as JsonArray;
        if (issues is null)
        {
            return [];
        }

        return issues
            .Select(ParseIssue)
            .Where(issue => issue is not null)
            .Cast<JiraIssue>()
            .OrderBy(issue => issue.Key, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<IReadOnlyList<JiraIssue>> SearchByIssuePropertyAsync(
        string projectKey,
        string propertyKey,
        string commitSha,
        CancellationToken cancellationToken = default)
    {
        var escapedProjectKey = EscapeJqlLiteral(projectKey);
        var escapedPropertyKey = propertyKey.Replace("\"", "\\\"", StringComparison.Ordinal);
        var escapedSha = commitSha.Replace("\"", "\\\"", StringComparison.Ordinal);
        var jql = $"project = \"{escapedProjectKey}\" AND issue.property[\"{escapedPropertyKey}\"].value = \"{escapedSha}\"";
        return await SearchAsync(jql, 500, DefaultSearchFields(), cancellationToken);
    }

    public async Task<IReadOnlyList<JiraIssue>> SearchByCommitLabelAsync(
        string projectKey,
        string commitLabel,
        CancellationToken cancellationToken = default)
    {
        var escapedProjectKey = EscapeJqlLiteral(projectKey);
        var escapedLabel = commitLabel.Replace("\"", "\\\"", StringComparison.Ordinal);
        var jql = $"project = \"{escapedProjectKey}\" AND labels = \"{escapedLabel}\"";
        return await SearchAsync(jql, 500, DefaultSearchFields(), cancellationToken);
    }

    public async Task<string> CreateIssueAsync(JsonObject fields, CancellationToken cancellationToken = default)
    {
        var payload = new JsonObject { ["fields"] = fields };
        var response = await SendAsync(HttpMethod.Post, "/rest/api/3/issue", payload, cancellationToken);
        var root = JsonNode.Parse(response) as JsonObject;
        var key = root?["key"]?.GetValue<string?>();
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("Jira create issue response did not include issue key.");
        }

        return key;
    }

    public async Task UpdateIssueAsync(string issueKey, JsonObject fields, CancellationToken cancellationToken = default)
    {
        var payload = new JsonObject { ["fields"] = fields };
        await SendAsync(HttpMethod.Put, $"/rest/api/3/issue/{issueKey}", payload, cancellationToken);
    }

    public async Task SetIssuePropertyAsync(string issueKey, string propertyKey, string commitSha, CancellationToken cancellationToken = default)
    {
        var payload = new JsonObject
        {
            ["value"] = commitSha,
            ["updated_at"] = DateTimeOffset.UtcNow.ToString("O"),
        };

        await SendAsync(HttpMethod.Put, $"/rest/api/3/issue/{issueKey}/properties/{propertyKey}", payload, cancellationToken);
    }

    public static IReadOnlyList<string> DefaultSearchFields()
        => ["summary", "status", "assignee", "updated", "labels", "issuetype"];

    private async Task<string> SendAsync(HttpMethod method, string path, JsonObject? payload, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        if (payload is not null)
        {
            request.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Jira API request failed ({(int)response.StatusCode} {response.ReasonPhrase}): {body}");
        }

        return body;
    }

    private static JiraIssue? ParseIssue(JsonNode? issueNode)
    {
        var issue = issueNode as JsonObject;
        if (issue is null)
        {
            return null;
        }

        var key = issue["key"]?.GetValue<string?>() ?? string.Empty;
        var fields = issue["fields"] as JsonObject;
        var summary = fields?["summary"]?.GetValue<string?>() ?? string.Empty;
        var status = (fields?["status"] as JsonObject)?["name"]?.GetValue<string?>() ?? string.Empty;
        var issueType = (fields?["issuetype"] as JsonObject)?["name"]?.GetValue<string?>() ?? string.Empty;
        var assignee = (fields?["assignee"] as JsonObject)?["displayName"]?.GetValue<string?>();
        var updated = fields?["updated"]?.GetValue<string?>();

        var labels = new List<string>();
        if (fields?["labels"] is JsonArray labelsArray)
        {
            foreach (var labelNode in labelsArray)
            {
                var label = labelNode?.GetValue<string?>();
                if (!string.IsNullOrWhiteSpace(label))
                {
                    labels.Add(label);
                }
            }
        }

        return new JiraIssue
        {
            Key = key,
            Summary = summary,
            Status = status,
            IssueType = issueType,
            Assignee = assignee,
            Updated = updated,
            Labels = labels,
        };
    }

    private static HttpClient BuildAuthenticatedClient(JiraConfig config)
    {
        var email = Environment.GetEnvironmentVariable(config.Jira.Auth.EmailEnvVar);
        var token = Environment.GetEnvironmentVariable(config.Jira.Auth.ApiTokenEnvVar);

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException(
                $"Missing Jira credentials. Set env vars '{config.Jira.Auth.EmailEnvVar}' and '{config.Jira.Auth.ApiTokenEnvVar}'.");
        }

        var client = new HttpClient();
        client.BaseAddress = new Uri(config.Jira.Url.TrimEnd('/') + "/");

        var authRaw = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{token}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authRaw);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private static string EscapeJqlLiteral(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    public void Dispose()
    {
        if (_disposeClient)
        {
            _httpClient.Dispose();
        }
    }
}
