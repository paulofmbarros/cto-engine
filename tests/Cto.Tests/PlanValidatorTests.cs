using Cto.Core.Validation;

namespace Cto.Tests;

public sealed class PlanValidatorTests
{
    [Fact]
    public async Task RejectsNonFibonacciEstimate()
    {
        var path = WriteTempPlan("""
version: "1.0"
goal: "Ship reliable weekly planning engine"
success_criteria:
  - "At least one metric is improved this week"
risks: []
work_breakdown:
  - epic_key: ""
    summary: "Execution core"
    stories:
      - summary: "Implement CLI contract"
        objective: "Provide a stable command surface for weekly operation."
        scope:
          in_scope: ["Command parsing"]
          out_of_scope: ["GUI"]
        acceptance_criteria: ["Command returns exit codes"]
        definition_of_done: ["Tests pass"]
        estimate: 4
assumptions: ["Users run this weekly"]
""");

        var validator = new PlanValidator();
        var result = await validator.ValidateAsync(path);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "PLAN_STORY_ESTIMATE_FIBONACCI");
    }

    [Fact]
    public async Task RejectsUnknownFieldsInStrictSchema()
    {
        var path = WriteTempPlan("""
version: "1.0"
goal: "Ship reliable weekly planning engine"
success_criteria:
  - "At least one metric is improved this week"
risks: []
work_breakdown:
  - epic_key: ""
    summary: "Execution core"
    extra_field: "not allowed"
    stories:
      - summary: "Implement CLI contract"
        objective: "Provide a stable command surface for weekly operation."
        scope:
          in_scope: ["Command parsing"]
          out_of_scope: ["GUI"]
        acceptance_criteria: ["Command returns exit codes"]
        definition_of_done: ["Tests pass"]
        estimate: 3
assumptions: ["Users run this weekly"]
""");

        var validator = new PlanValidator();
        var result = await validator.ValidateAsync(path);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "PLAN_UNKNOWN_FIELD");
    }

    [Fact]
    public async Task RejectsPlaceholderStoryDependencies()
    {
        var path = WriteTempPlan("""
version: "1.0"
goal: "Ship reliable weekly planning engine"
success_criteria:
  - "At least one metric is improved this week"
risks: []
work_breakdown:
  - epic_key: ""
    summary: "Execution core"
    stories:
      - summary: "Implement CLI contract"
        objective: "Provide a stable command surface for weekly operation."
        scope:
          in_scope: ["Command parsing"]
          out_of_scope: ["GUI"]
        acceptance_criteria: ["Command returns exit codes"]
        definition_of_done: ["Tests pass"]
        estimate: 3
        dependencies: ["PROJ-XXX"]
assumptions: ["Users run this weekly"]
""");

        var validator = new PlanValidator();
        var result = await validator.ValidateAsync(path);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "PLAN_STORY_DEPENDENCY_FORMAT");
    }

    private static string WriteTempPlan(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"plan-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, content);
        return path;
    }
}
