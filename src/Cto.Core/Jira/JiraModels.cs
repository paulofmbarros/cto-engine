namespace Cto.Core.Jira;

public sealed class JiraIssue
{
    public string Key { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? Assignee { get; init; }
    public string? Updated { get; init; }
    public string IssueType { get; init; } = string.Empty;
    public IReadOnlyList<string> Labels { get; init; } = [];
}
