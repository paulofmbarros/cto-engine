using Cto.Core.Common;
using System.Text.RegularExpressions;

namespace Cto.Core.Validation;

public sealed class LlmConfigValidator
{
    public ValidationResult ValidateConfig(LlmConfig config, string pathHint = "$.llm-config")
    {
        var result = new ValidationResult();

        var provider = config.Provider.Trim().ToLowerInvariant();
        if (provider != "gemini")
        {
            result.AddError("LLM_PROVIDER", "provider must be 'gemini'.", pathHint);
        }

        if (string.IsNullOrWhiteSpace(config.Providers.Gemini.ApiKeyEnvVar))
        {
            result.AddError("LLM_API_KEY_ENV", "providers.gemini.api_key_env_var is required.", pathHint);
        }

        if (string.IsNullOrWhiteSpace(config.Providers.Gemini.Model))
        {
            result.AddError("LLM_MODEL", "providers.gemini.model is required.", pathHint);
        }

        if (string.IsNullOrWhiteSpace(config.Providers.Gemini.BaseUrl))
        {
            result.AddError("LLM_BASE_URL", "providers.gemini.base_url is required.", pathHint);
        }

        if (config.Generation.Candidates < 1 || config.Generation.Candidates > 5)
        {
            result.AddError("LLM_CANDIDATES_RANGE", "generation.candidates must be between 1 and 5.", pathHint);
        }

        if (config.Generation.MaxOutputTokensPerCandidate < 256)
        {
            result.AddError("LLM_MAX_OUTPUT_TOKENS", "generation.max_output_tokens_per_candidate must be >= 256.", pathHint);
        }

        if (config.Budget.MaxRequestsPerRun < 1)
        {
            result.AddError("LLM_BUDGET_REQUESTS", "budget.max_requests_per_run must be >= 1.", pathHint);
        }

        if (config.Budget.MaxTotalOutputTokens < 512)
        {
            result.AddError("LLM_BUDGET_TOKENS", "budget.max_total_output_tokens must be >= 512.", pathHint);
        }

        if (config.Budget.MaxCostUsdPerRun <= 0)
        {
            result.AddError("LLM_BUDGET_COST", "budget.max_cost_usd_per_run must be > 0.", pathHint);
        }

        if (!config.Output.CandidateFilePattern.Contains("{n}", StringComparison.Ordinal))
        {
            result.AddError("LLM_CANDIDATE_PATTERN", "output.candidate_file_pattern must include '{n}'.", pathHint);
        }

        if (config.Validation.RequireMinValidCandidates < 1)
        {
            result.AddError("LLM_MIN_VALID", "validation.require_min_valid_candidates must be >= 1.", pathHint);
        }

        if (config.Validation.RequireMinValidCandidates > config.Generation.Candidates)
        {
            result.AddError("LLM_MIN_VALID_RANGE", "validation.require_min_valid_candidates cannot exceed generation.candidates.", pathHint);
        }

        return result;
    }

    public async Task<(ValidationResult Result, LlmConfig? Config)> ValidateAsync(
        string llmConfigPath,
        CancellationToken cancellationToken = default)
    {
        var result = new ValidationResult();
        if (!File.Exists(llmConfigPath))
        {
            result.AddError("LLM_CONFIG_MISSING", "llm-config.yaml is missing.", llmConfigPath);
            return (result, null);
        }

        var (config, error) = await YamlBridge.LoadAsAsync<LlmConfig>(llmConfigPath, cancellationToken);
        if (error is not null)
        {
            result.AddError("LLM_CONFIG_PARSE", error, llmConfigPath);
            return (result, null);
        }

        if (config is null)
        {
            result.AddError("LLM_CONFIG_EMPTY", "llm-config.yaml is empty or invalid.", llmConfigPath);
            return (result, null);
        }

        var rawContent = await File.ReadAllTextAsync(llmConfigPath, cancellationToken);
        if (Regex.IsMatch(rawContent, @"(?im)^\s*api_key\s*:"))
        {
            result.AddError("LLM_API_KEY_INLINE", "Inline API keys are forbidden. Use providers.gemini.api_key_env_var and environment variables.", llmConfigPath);
        }
        result.Merge(ValidateConfig(config, llmConfigPath));

        return (result, config);
    }
}
