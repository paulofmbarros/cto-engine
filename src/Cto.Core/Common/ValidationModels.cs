using System.Text;

namespace Cto.Core.Common;

public enum ValidationSeverity
{
    Warning,
    Error,
}

public sealed record ValidationIssue(
    ValidationSeverity Severity,
    string Code,
    string Message,
    string? Path = null)
{
    public override string ToString()
    {
        var prefix = Severity == ValidationSeverity.Error ? "ERROR" : "WARN";
        return string.IsNullOrWhiteSpace(Path)
            ? $"[{prefix}] {Code}: {Message}"
            : $"[{prefix}] {Code} ({Path}): {Message}";
    }
}

public sealed class ValidationResult
{
    public List<ValidationIssue> Issues { get; } = [];

    public bool IsValid => Issues.All(i => i.Severity != ValidationSeverity.Error);

    public void AddError(string code, string message, string? path = null)
        => Issues.Add(new ValidationIssue(ValidationSeverity.Error, code, message, path));

    public void AddWarning(string code, string message, string? path = null)
        => Issues.Add(new ValidationIssue(ValidationSeverity.Warning, code, message, path));

    public void Merge(ValidationResult other)
    {
        foreach (var issue in other.Issues)
        {
            Issues.Add(issue);
        }
    }

    public string ToDisplayString()
    {
        var builder = new StringBuilder();
        foreach (var issue in Issues)
        {
            builder.AppendLine(issue.ToString());
        }

        builder.AppendLine(IsValid ? "Validation passed." : "Validation failed.");
        return builder.ToString().TrimEnd();
    }
}
