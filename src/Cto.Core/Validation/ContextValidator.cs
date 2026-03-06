using System.Text.RegularExpressions;
using Cto.Core.Common;

namespace Cto.Core.Validation;

public sealed class ContextValidator
{
    private static readonly string[] RequiredHeadings =
    [
        "## What exists? (Production State)",
        "## What's in flight? (Current Work)",
        "## What's broken? (Known Issues)",
        "## What changed? (Since Last Week)",
    ];

    public ValidationResult Validate(string contextPath)
    {
        var result = new ValidationResult();
        if (!File.Exists(contextPath))
        {
            result.AddError("CONTEXT_MISSING", "context.md is missing.", contextPath);
            return result;
        }

        var content = File.ReadAllText(contextPath);
        foreach (var heading in RequiredHeadings)
        {
            if (!content.Contains(heading, StringComparison.Ordinal))
            {
                result.AddError("CONTEXT_HEADING_MISSING", $"Missing required heading: {heading}", contextPath);
            }
        }

        if (!result.IsValid)
        {
            return result;
        }

        var existsSection = ExtractSection(content, RequiredHeadings[0]);
        var brokenSection = ExtractSection(content, RequiredHeadings[2]);

        if (!ContainsListItem(existsSection))
        {
            result.AddError("CONTEXT_EXISTS_EMPTY", "'What exists?' must include at least one list item.", contextPath);
        }

        if (!Regex.IsMatch(content, @"\d"))
        {
            result.AddError("CONTEXT_METRICS", "Context must include quantified metrics (at least one numeric value).", contextPath);
        }

        if (!Regex.IsMatch(brokenSection, "(?i)blocker|none"))
        {
            result.AddError("CONTEXT_BLOCKERS", "'What's broken?' must explicitly mention blockers or state 'None'.", contextPath);
        }

        return result;
    }

    private static bool ContainsListItem(string content)
        => Regex.IsMatch(content, @"(?m)^\s*(-|\d+\.)\s+");

    private static string ExtractSection(string markdown, string heading)
    {
        var start = markdown.IndexOf(heading, StringComparison.Ordinal);
        if (start < 0)
        {
            return string.Empty;
        }

        var sectionStart = start + heading.Length;
        var nextHeading = markdown.IndexOf("\n## ", sectionStart, StringComparison.Ordinal);
        var end = nextHeading < 0 ? markdown.Length : nextHeading;
        return markdown[sectionStart..end].Trim();
    }
}
