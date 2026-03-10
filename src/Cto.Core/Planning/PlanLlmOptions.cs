namespace Cto.Core.Planning;

public sealed class PlanLlmOptions
{
    public string? Provider { get; init; }
    public int? Candidates { get; init; }
    public string? VisionFile { get; init; }
    public int? MaxInputTokens { get; init; }
    public int? MaxOutputTokensPerCandidate { get; init; }
    public double? BudgetUsd { get; init; }
}
