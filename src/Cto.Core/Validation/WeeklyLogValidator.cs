using System.Text.RegularExpressions;
using Cto.Core.Common;

namespace Cto.Core.Validation;

public sealed class WeeklyLogValidator
{
    private static readonly string[] RequiredHeadings =
    [
        "## What surprised you this week?",
        "## What assumption broke?",
        "## What did users actually do?",
        "## What blocked you?",
        "## What did you learn?",
    ];

    private static readonly string[] NoneStatements =
    [
        "nothing significant",
        "no assumptions broke",
        "behavior matched expectations",
        "no user data this week",
        "no blockers",
        "no significant learnings",
    ];

    public ValidationResult Validate(string weeklyLogPath)
    {
        var result = new ValidationResult();
        if (!File.Exists(weeklyLogPath))
        {
            result.AddError("WEEKLYLOG_MISSING", "weeklylog.md is missing.", weeklyLogPath);
            return result;
        }

        var content = File.ReadAllText(weeklyLogPath);
        foreach (var heading in RequiredHeadings)
        {
            if (!content.Contains(heading, StringComparison.Ordinal))
            {
                result.AddError("WEEKLYLOG_HEADING_MISSING", $"Missing required heading: {heading}", weeklyLogPath);
            }
        }

        if (!result.IsValid)
        {
            return result;
        }

        foreach (var heading in RequiredHeadings)
        {
            var section = ExtractSection(content, heading);
            if (string.IsNullOrWhiteSpace(section))
            {
                result.AddError("WEEKLYLOG_SECTION_EMPTY", $"Section '{heading}' cannot be empty.", weeklyLogPath);
                continue;
            }

            if (!ContainsListItem(section) && !ContainsNoneStatement(section))
            {
                result.AddError("WEEKLYLOG_SECTION_STRUCTURE", $"Section '{heading}' must include at least one structured entry or explicit none statement.", weeklyLogPath);
            }
        }

        var blockers = ExtractSection(content, "## What blocked you?");
        if (!ContainsNoneStatement(blockers) && !Regex.IsMatch(blockers, "(?i)(\\d+\\s*(hour|hours|h|day|days|min|mins|minutes))"))
        {
            result.AddError("WEEKLYLOG_BLOCKER_TIME", "Blockers section must quantify time impact (e.g., '6 hours').", weeklyLogPath);
        }

        return result;
    }

    private static bool ContainsListItem(string content)
        => Regex.IsMatch(content, @"(?m)^\s*(-|\d+\.)\s+");

    private static bool ContainsNoneStatement(string content)
        => NoneStatements.Any(none => content.Contains(none, StringComparison.OrdinalIgnoreCase));

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
