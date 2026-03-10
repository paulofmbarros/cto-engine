using Cto.Core.Validation;

namespace Cto.Tests;

public sealed class LlmConfigValidatorTests
{
    [Fact]
    public async Task AcceptsValidGeminiConfig()
    {
        var path = WriteTemp("""
version: "1.0"
provider: "gemini"
providers:
  gemini:
    api_key_env_var: "GEMINI_API_KEY"
    model: "gemini-2.0-flash"
    base_url: "https://generativelanguage.googleapis.com"
generation:
  candidates: 3
  temperature: 0.3
  top_p: 0.9
  max_output_tokens_per_candidate: 3000
inputs:
  required_files:
    - "charter.md"
    - "context.md"
    - "weeklylog.md"
    - ".cto-engine/reality-check.md"
    - ".cto-engine/challenge-log.yaml"
  optional_files: []
output:
  proposal_dir_pattern: "docs/proposals/{iso_year}-W{iso_week}"
  candidate_file_pattern: "candidate-{n}.plan.yaml"
  summary_file: "candidates-summary.md"
validation:
  run_plan_validator: true
  require_min_valid_candidates: 1
  auto_repair_attempts: 0
budget:
  max_requests_per_run: 3
  max_total_output_tokens: 12000
  max_cost_usd_per_run: 2.5
retry:
  max_retries: 2
  initial_backoff_ms: 700
  max_backoff_ms: 4000
logging:
  enabled: true
  log_dir: ".cto-engine/logs"
  persist_raw_llm_response: false
  include_token_usage: true
  include_prompt_hash: true
security:
  redact_env_vars: ["GEMINI_API_KEY"]
  store_full_prompt: false
""");

        var validator = new LlmConfigValidator();
        var (result, config) = await validator.ValidateAsync(path);

        Assert.True(result.IsValid);
        Assert.NotNull(config);
    }

    [Fact]
    public async Task RejectsInvalidCandidatePattern()
    {
        var path = WriteTemp("""
version: "1.0"
provider: "gemini"
providers:
  gemini:
    api_key_env_var: "GEMINI_API_KEY"
    model: "gemini-2.0-flash"
    base_url: "https://generativelanguage.googleapis.com"
generation:
  candidates: 2
  max_output_tokens_per_candidate: 3000
inputs:
  required_files: ["charter.md"]
  optional_files: []
output:
  proposal_dir_pattern: "docs/proposals/{iso_year}-W{iso_week}"
  candidate_file_pattern: "candidate.plan.yaml"
  summary_file: "candidates-summary.md"
validation:
  run_plan_validator: true
  require_min_valid_candidates: 1
budget:
  max_requests_per_run: 2
  max_total_output_tokens: 9000
  max_cost_usd_per_run: 1.5
retry:
  max_retries: 2
  initial_backoff_ms: 700
  max_backoff_ms: 4000
logging:
  enabled: true
  log_dir: ".cto-engine/logs"
security:
  redact_env_vars: ["GEMINI_API_KEY"]
  store_full_prompt: false
""");

        var validator = new LlmConfigValidator();
        var (result, _) = await validator.ValidateAsync(path);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "LLM_CANDIDATE_PATTERN");
    }

    [Fact]
    public async Task RejectsInlineApiKey()
    {
        var path = WriteTemp("""
version: "1.0"
provider: "gemini"
providers:
  gemini:
    api_key: "hardcoded-secret"
    api_key_env_var: "GEMINI_API_KEY"
    model: "gemini-2.0-flash"
    base_url: "https://generativelanguage.googleapis.com"
generation:
  candidates: 1
  max_output_tokens_per_candidate: 1000
inputs:
  required_files: ["charter.md"]
  optional_files: []
output:
  proposal_dir_pattern: "docs/proposals/{iso_year}-W{iso_week}"
  candidate_file_pattern: "candidate-{n}.plan.yaml"
  summary_file: "candidates-summary.md"
validation:
  run_plan_validator: true
  require_min_valid_candidates: 1
budget:
  max_requests_per_run: 1
  max_total_output_tokens: 5000
  max_cost_usd_per_run: 1
retry:
  max_retries: 1
  initial_backoff_ms: 500
  max_backoff_ms: 1000
logging:
  enabled: true
  log_dir: ".cto-engine/logs"
security:
  redact_env_vars: ["GEMINI_API_KEY"]
  store_full_prompt: false
""");

        var validator = new LlmConfigValidator();
        var (result, _) = await validator.ValidateAsync(path);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "LLM_API_KEY_INLINE");
    }

    private static string WriteTemp(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"llm-config-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, content);
        return path;
    }
}
