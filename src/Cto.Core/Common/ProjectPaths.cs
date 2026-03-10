namespace Cto.Core.Common;

public sealed class ProjectPaths
{
    public required string Root { get; init; }
    public required string CharterPath { get; init; }
    public required string ContextPath { get; init; }
    public required string WeeklyLogPath { get; init; }
    public required string PlanPath { get; init; }
    public required string CtoEngineDirectory { get; init; }
    public required string JiraConfigPath { get; init; }
    public required string LlmConfigPath { get; init; }
    public required string ChallengeLogPath { get; init; }
    public required string SnapshotPath { get; init; }
    public required string RealityCheckPath { get; init; }
    public required string LogsDirectory { get; init; }
    public required string ProposalsDirectory { get; init; }

    public static ProjectPaths FromRoot(string projectRoot)
    {
        var root = Path.GetFullPath(projectRoot);
        var engineDir = Path.Combine(root, ".cto-engine");

        return new ProjectPaths
        {
            Root = root,
            CharterPath = Path.Combine(root, "charter.md"),
            ContextPath = Path.Combine(root, "context.md"),
            WeeklyLogPath = Path.Combine(root, "weeklylog.md"),
            PlanPath = Path.Combine(root, "plan.yaml"),
            CtoEngineDirectory = engineDir,
            JiraConfigPath = Path.Combine(engineDir, "jira-config.yaml"),
            LlmConfigPath = Path.Combine(engineDir, "llm-config.yaml"),
            ChallengeLogPath = Path.Combine(engineDir, "challenge-log.yaml"),
            SnapshotPath = Path.Combine(engineDir, "snapshot.json"),
            RealityCheckPath = Path.Combine(engineDir, "reality-check.md"),
            LogsDirectory = Path.Combine(engineDir, "logs"),
            ProposalsDirectory = Path.Combine(root, "docs", "proposals"),
        };
    }
}
