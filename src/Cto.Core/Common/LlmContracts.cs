using System.Text.Json.Serialization;

namespace Cto.Core.Common;

public sealed class LlmConfig
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("provider")]
    public string Provider { get; set; } = "gemini";

    [JsonPropertyName("providers")]
    public LlmProviders Providers { get; set; } = new();

    [JsonPropertyName("generation")]
    public LlmGenerationConfig Generation { get; set; } = new();

    [JsonPropertyName("inputs")]
    public LlmInputsConfig Inputs { get; set; } = new();

    [JsonPropertyName("output")]
    public LlmOutputConfig Output { get; set; } = new();

    [JsonPropertyName("validation")]
    public LlmValidationConfig Validation { get; set; } = new();

    [JsonPropertyName("budget")]
    public LlmBudgetConfig Budget { get; set; } = new();

    [JsonPropertyName("retry")]
    public LlmRetryConfig Retry { get; set; } = new();

    [JsonPropertyName("logging")]
    public LlmLoggingConfig Logging { get; set; } = new();

    [JsonPropertyName("security")]
    public LlmSecurityConfig Security { get; set; } = new();
}

public sealed class LlmProviders
{
    [JsonPropertyName("gemini")]
    public GeminiProviderConfig Gemini { get; set; } = new();
}

public sealed class GeminiProviderConfig
{
    [JsonPropertyName("api_key_env_var")]
    public string ApiKeyEnvVar { get; set; } = "GEMINI_API_KEY";

    [JsonPropertyName("model")]
    public string Model { get; set; } = "gemini-2.0-flash";

    [JsonPropertyName("base_url")]
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com";
}

public sealed class LlmGenerationConfig
{
    [JsonPropertyName("candidates")]
    public int Candidates { get; set; } = 3;

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; } = 0.4;

    [JsonPropertyName("top_p")]
    public double TopP { get; set; } = 0.9;

    [JsonPropertyName("max_output_tokens_per_candidate")]
    public int MaxOutputTokensPerCandidate { get; set; } = 3500;

    [JsonPropertyName("stop_on_first_valid")]
    public bool StopOnFirstValid { get; set; }
}

public sealed class LlmInputsConfig
{
    [JsonPropertyName("required_files")]
    public List<string> RequiredFiles { get; set; } =
    [
        "charter.md",
        "context.md",
        "weeklylog.md",
        ".cto-engine/reality-check.md",
        ".cto-engine/challenge-log.yaml",
    ];

    [JsonPropertyName("optional_files")]
    public List<string> OptionalFiles { get; set; } = [];
}

public sealed class LlmOutputConfig
{
    [JsonPropertyName("proposal_dir_pattern")]
    public string ProposalDirPattern { get; set; } = "docs/proposals/{iso_year}-W{iso_week}";

    [JsonPropertyName("candidate_file_pattern")]
    public string CandidateFilePattern { get; set; } = "candidate-{n}.plan.yaml";

    [JsonPropertyName("summary_file")]
    public string SummaryFile { get; set; } = "candidates-summary.md";
}

public sealed class LlmValidationConfig
{
    [JsonPropertyName("run_plan_validator")]
    public bool RunPlanValidator { get; set; } = true;

    [JsonPropertyName("require_min_valid_candidates")]
    public int RequireMinValidCandidates { get; set; } = 1;

    [JsonPropertyName("auto_repair_attempts")]
    public int AutoRepairAttempts { get; set; }
}

public sealed class LlmBudgetConfig
{
    [JsonPropertyName("max_requests_per_run")]
    public int MaxRequestsPerRun { get; set; } = 6;

    [JsonPropertyName("max_total_output_tokens")]
    public int MaxTotalOutputTokens { get; set; } = 30000;

    [JsonPropertyName("max_cost_usd_per_run")]
    public double MaxCostUsdPerRun { get; set; } = 3.0;
}

public sealed class LlmRetryConfig
{
    [JsonPropertyName("max_retries")]
    public int MaxRetries { get; set; } = 3;

    [JsonPropertyName("initial_backoff_ms")]
    public int InitialBackoffMs { get; set; } = 750;

    [JsonPropertyName("max_backoff_ms")]
    public int MaxBackoffMs { get; set; } = 5000;
}

public sealed class LlmLoggingConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("log_dir")]
    public string LogDir { get; set; } = ".cto-engine/logs";

    [JsonPropertyName("persist_raw_llm_response")]
    public bool PersistRawLlmResponse { get; set; }

    [JsonPropertyName("include_token_usage")]
    public bool IncludeTokenUsage { get; set; } = true;

    [JsonPropertyName("include_prompt_hash")]
    public bool IncludePromptHash { get; set; } = true;
}

public sealed class LlmSecurityConfig
{
    [JsonPropertyName("redact_env_vars")]
    public List<string> RedactEnvVars { get; set; } = ["GEMINI_API_KEY", "JIRA_API_TOKEN"];

    [JsonPropertyName("store_full_prompt")]
    public bool StoreFullPrompt { get; set; }
}

public sealed class LlmGenerationResult
{
    public required string Provider { get; init; }
    public required string Model { get; init; }
    public required string RawText { get; init; }
    public LlmUsage? Usage { get; init; }
}

public sealed class LlmUsage
{
    public int PromptTokens { get; init; }
    public int OutputTokens { get; init; }
    public int TotalTokens { get; init; }
}

public sealed class ProposedPlanCandidate
{
    public required int Index { get; init; }
    public required string Name { get; init; }
    public required string StrategySummary { get; init; }
    public required string PlanYaml { get; init; }
    public string? Rationale { get; init; }
    public ProposedPlanScores Scores { get; init; } = new();
}

public sealed class ProposedPlanScores
{
    public double? StrategicAlignment { get; init; }
    public double? DeliveryRisk { get; init; }
    public double? DependencyRisk { get; init; }
    public double? EffortRealism { get; init; }
}
