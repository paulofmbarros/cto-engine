using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cto.Core.Common;
using Cto.Core.LLM;
using Cto.Core.Validation;

namespace Cto.Core.Planning;

public sealed class PlanProposalService
{
    private readonly ILlmClient _llmClient;
    private readonly LlmConfigValidator _llmConfigValidator = new();
    private readonly PlanValidator _planValidator = new();

    public PlanProposalService(ILlmClient? llmClient = null)
    {
        _llmClient = llmClient ?? new GeminiClient();
    }

    public async Task<OperationResult> GenerateAsync(
        ProjectPaths paths,
        PlanLlmOptions options,
        CancellationToken cancellationToken = default)
    {
        var validation = await _llmConfigValidator.ValidateAsync(paths.LlmConfigPath, cancellationToken);
        if (!validation.Result.IsValid || validation.Config is null)
        {
            var messages = validation.Result.Issues.Select(i => i.ToString()).ToList();
            messages.Insert(0, "LLM planning blocked: llm-config validation failed.");
            return OperationResult.Fail(messages);
        }

        var config = ApplyOverrides(validation.Config, options);
        var effectiveValidation = _llmConfigValidator.ValidateConfig(config, "cli_overrides");
        if (!effectiveValidation.IsValid)
        {
            var messages = effectiveValidation.Issues.Select(i => i.ToString()).ToList();
            messages.Insert(0, "LLM planning blocked: CLI options produced invalid llm configuration.");
            return OperationResult.Fail(messages);
        }

        if (!string.Equals(config.Provider, "gemini", StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult.Fail("LLM planning blocked: only provider 'gemini' is supported in this version.");
        }

        var requiredInputs = ResolveRequiredInputs(paths.Root, config.Inputs.RequiredFiles, options.VisionFile);
        var missingInputs = requiredInputs.Where(file => !File.Exists(file.Path)).ToList();
        if (missingInputs.Count > 0)
        {
            return OperationResult.Fail(missingInputs.Select(file => $"Missing required planning input for LLM: {file.Path}"));
        }

        var optionalInputs = ResolveOptionalInputs(paths.Root, config.Inputs.OptionalFiles);
        var allInputs = requiredInputs.Concat(optionalInputs.Where(file => File.Exists(file.Path))).ToList();

        var maxInputTokens = options.MaxInputTokens.GetValueOrDefault(12000);
        var prompt = await BuildPromptAsync(allInputs, config.Generation.Candidates, maxInputTokens, cancellationToken);
        var promptHash = ComputeSha256(prompt);

        var estimatedInputTokens = EstimateTokens(prompt);
        if (estimatedInputTokens > maxInputTokens)
        {
            return OperationResult.Fail(
                $"LLM planning blocked: estimated input tokens ({estimatedInputTokens}) exceed max-input-tokens ({maxInputTokens}).");
        }

        var maxOutputTokensPerCandidate = config.Generation.MaxOutputTokensPerCandidate;
        var maxTotalOutputTokens = maxOutputTokensPerCandidate * config.Generation.Candidates;
        if (maxTotalOutputTokens > config.Budget.MaxTotalOutputTokens)
        {
            return OperationResult.Fail(
                $"LLM planning blocked: configured output token budget ({maxTotalOutputTokens}) exceeds budget.max_total_output_tokens ({config.Budget.MaxTotalOutputTokens}).");
        }

        var generation = await _llmClient.GeneratePlansAsync(
            prompt,
            config.Providers.Gemini,
            config.Generation,
            cancellationToken);

        if (generation.Usage is not null && generation.Usage.OutputTokens > config.Budget.MaxTotalOutputTokens)
        {
            return OperationResult.Fail(
                $"LLM planning blocked: model output tokens ({generation.Usage.OutputTokens}) exceeded budget.max_total_output_tokens ({config.Budget.MaxTotalOutputTokens}).");
        }

        var parsedCandidates = ParseCandidates(generation.RawText);
        if (parsedCandidates.Count == 0)
        {
            await WriteLlmLogAsync(paths, config, promptHash, generation, allInputs, [], cancellationToken);
            return OperationResult.Fail("LLM planning failed: provider response did not include any parseable candidate.");
        }

        var now = DateTime.UtcNow;
        var proposalDir = ResolveProposalDirectory(paths, config.Output, now);
        Directory.CreateDirectory(proposalDir);

        var outcomes = new List<CandidateOutcome>();
        var targetCandidates = Math.Min(config.Generation.Candidates, parsedCandidates.Count);

        for (var i = 0; i < targetCandidates; i++)
        {
            var candidateBase = parsedCandidates[i];
            var candidate = new ProposedPlanCandidate
            {
                Index = i + 1,
                Name = candidateBase.Name,
                StrategySummary = candidateBase.StrategySummary,
                PlanYaml = candidateBase.PlanYaml,
                Rationale = candidateBase.Rationale,
                Scores = candidateBase.Scores,
            };
            var candidateFile = ResolveCandidateFileName(config.Output.CandidateFilePattern, candidate.Index);
            var candidatePath = Path.Combine(proposalDir, candidateFile);
            await File.WriteAllTextAsync(candidatePath, EnsureTrailingNewLine(candidate.PlanYaml), cancellationToken);

            var validationResult = config.Validation.RunPlanValidator
                ? await _planValidator.ValidateAsync(candidatePath, cancellationToken)
                : new ValidationResult();

            var (plan, _) = await YamlBridge.LoadAsAsync<PlanDocument>(candidatePath, cancellationToken);
            var stories = plan?.WorkBreakdown.Sum(w => w.Stories.Count) ?? 0;
            var points = plan?.WorkBreakdown.Sum(w => w.Stories.Sum(s => s.Estimate)) ?? 0;

            outcomes.Add(new CandidateOutcome
            {
                Index = candidate.Index,
                Name = candidate.Name,
                StrategySummary = candidate.StrategySummary,
                Rationale = candidate.Rationale,
                Scores = candidate.Scores,
                FileName = candidateFile,
                FilePath = candidatePath,
                IsValid = validationResult.IsValid,
                ValidationIssues = validationResult.Issues,
                StoryCount = stories,
                StoryPoints = points,
            });
        }

        var summaryPath = Path.Combine(proposalDir, config.Output.SummaryFile);
        var summary = BuildSummaryMarkdown(now, allInputs, outcomes);
        await File.WriteAllTextAsync(summaryPath, summary, cancellationToken);

        await WriteLlmLogAsync(paths, config, promptHash, generation, allInputs, outcomes, cancellationToken);

        var validCount = outcomes.Count(outcome => outcome.IsValid);
        if (validCount < config.Validation.RequireMinValidCandidates)
        {
            var message =
                $"LLM planning generated {outcomes.Count} candidate(s) but only {validCount} passed plan validation. " +
                $"Minimum required: {config.Validation.RequireMinValidCandidates}.";
            return OperationResult.Fail(message, $"See summary: {summaryPath}");
        }

        return OperationResult.Ok(
            $"Generated {outcomes.Count} plan candidate(s); {validCount} valid.",
            $"Candidates directory: {proposalDir}",
            $"Summary: {summaryPath}");
    }

    private static LlmConfig ApplyOverrides(LlmConfig source, PlanLlmOptions options)
    {
        var clone = new LlmConfig
        {
            Version = source.Version,
            Provider = string.IsNullOrWhiteSpace(options.Provider) ? source.Provider : options.Provider.Trim(),
            Providers = source.Providers,
            Generation = new LlmGenerationConfig
            {
                Candidates = options.Candidates.GetValueOrDefault(source.Generation.Candidates),
                Temperature = source.Generation.Temperature,
                TopP = source.Generation.TopP,
                MaxOutputTokensPerCandidate = options.MaxOutputTokensPerCandidate.GetValueOrDefault(source.Generation.MaxOutputTokensPerCandidate),
                StopOnFirstValid = source.Generation.StopOnFirstValid,
            },
            Inputs = source.Inputs,
            Output = source.Output,
            Validation = source.Validation,
            Budget = new LlmBudgetConfig
            {
                MaxRequestsPerRun = source.Budget.MaxRequestsPerRun,
                MaxTotalOutputTokens = source.Budget.MaxTotalOutputTokens,
                MaxCostUsdPerRun = options.BudgetUsd.GetValueOrDefault(source.Budget.MaxCostUsdPerRun),
            },
            Retry = source.Retry,
            Logging = source.Logging,
            Security = source.Security,
        };

        return clone;
    }

    private static List<InputFile> ResolveRequiredInputs(string projectRoot, IEnumerable<string> configuredFiles, string? visionFile)
    {
        var files = configuredFiles
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(file => new InputFile(file, ResolveInputPath(projectRoot, file), true))
            .ToList();

        if (!string.IsNullOrWhiteSpace(visionFile))
        {
            var normalizedVisionFile = visionFile!.Trim();
            var reference = Path.IsPathRooted(normalizedVisionFile)
                ? normalizedVisionFile
                : Path.Combine(projectRoot, normalizedVisionFile);
            files.Add(new InputFile(normalizedVisionFile, Path.GetFullPath(reference), true));
        }

        return files;
    }

    private static List<InputFile> ResolveOptionalInputs(string projectRoot, IEnumerable<string> configuredFiles)
    {
        return configuredFiles
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(file => new InputFile(file, ResolveInputPath(projectRoot, file), false))
            .ToList();
    }

    private static string ResolveInputPath(string projectRoot, string configuredPath)
    {
        var normalized = configuredPath.Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(projectRoot, normalized));
    }

    private static async Task<string> BuildPromptAsync(
        IReadOnlyList<InputFile> inputs,
        int candidateCount,
        int maxInputTokens,
        CancellationToken cancellationToken)
    {
        var instruction = """
You are an engineering strategy assistant. Produce exactly N distinct execution plan candidates for one weekly cycle.

Return ONLY valid JSON (no markdown fences, no commentary) with this shape:
{
  "candidates": [
    {
      "name": "short strategy name",
      "strategy_summary": "2-3 lines",
      "rationale": "main tradeoffs",
      "scores": {
        "strategic_alignment": 0.0,
        "delivery_risk": 0.0,
        "dependency_risk": 0.0,
        "effort_realism": 0.0
      },
      "plan_yaml": "full YAML document matching CTO Engine plan contract"
    }
  ]
}

Strict requirements:
- Return exactly N candidates.
- Every plan_yaml must be valid against CTO Engine constraints:
  - version format major.minor
  - fibonacci estimates only: 1,2,3,5,8,13
  - strict required fields in each story
  - dependency keys in Jira format (ABC-123) if provided
- Keep each candidate meaningfully different in sequencing and risk posture.
- Keep language concise and implementation-ready.
""";

        var targetChars = Math.Max(8000, maxInputTokens * 4);
        var budgetForInputs = Math.Max(2000, targetChars - instruction.Length - 2000);
        var perFileBudget = inputs.Count == 0 ? budgetForInputs : budgetForInputs / inputs.Count;

        var builder = new StringBuilder();
        builder.AppendLine(instruction.Trim());
        builder.AppendLine();
        builder.AppendLine($"N = {candidateCount}");
        builder.AppendLine();
        builder.AppendLine("# Inputs");
        builder.AppendLine();

        foreach (var input in inputs)
        {
            var content = await File.ReadAllTextAsync(input.Path, cancellationToken);
            var normalized = content.Trim();
            var clipped = Clip(normalized, perFileBudget);

            builder.AppendLine($"## {input.Reference}");
            builder.AppendLine();
            builder.AppendLine(clipped);
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    private static List<ProposedPlanCandidate> ParseCandidates(string rawText)
    {
        var rootNode = ParseJsonNode(rawText);
        if (rootNode is not JsonObject root || root["candidates"] is not JsonArray candidatesArray)
        {
            return [];
        }

        var results = new List<ProposedPlanCandidate>();
        var index = 0;
        foreach (var candidateNode in candidatesArray)
        {
            if (candidateNode is not JsonObject candidate)
            {
                continue;
            }

            index++;
            var name = candidate["name"]?.GetValue<string?>()?.Trim();
            var summary = candidate["strategy_summary"]?.GetValue<string?>()?.Trim();
            var planYaml = candidate["plan_yaml"]?.GetValue<string?>()?.Trim();
            var rationale = candidate["rationale"]?.GetValue<string?>();

            if (string.IsNullOrWhiteSpace(planYaml))
            {
                continue;
            }

            var scores = ParseScores(candidate["scores"] as JsonObject);
            results.Add(new ProposedPlanCandidate
            {
                Index = index,
                Name = string.IsNullOrWhiteSpace(name) ? $"Candidate {index}" : name,
                StrategySummary = string.IsNullOrWhiteSpace(summary) ? "No summary provided." : summary,
                PlanYaml = planYaml,
                Rationale = rationale,
                Scores = scores,
            });
        }

        return results;
    }

    private static ProposedPlanScores ParseScores(JsonObject? scores)
    {
        if (scores is null)
        {
            return new ProposedPlanScores();
        }

        return new ProposedPlanScores
        {
            StrategicAlignment = scores["strategic_alignment"]?.GetValue<double?>(),
            DeliveryRisk = scores["delivery_risk"]?.GetValue<double?>(),
            DependencyRisk = scores["dependency_risk"]?.GetValue<double?>(),
            EffortRealism = scores["effort_realism"]?.GetValue<double?>(),
        };
    }

    private static JsonNode? ParseJsonNode(string raw)
    {
        var direct = TryParse(raw);
        if (direct is not null)
        {
            return direct;
        }

        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }

        var slice = raw[start..(end + 1)];
        return TryParse(slice);
    }

    private static JsonNode? TryParse(string json)
    {
        try
        {
            return JsonNode.Parse(json);
        }
        catch
        {
            return null;
        }
    }

    private static string ResolveProposalDirectory(ProjectPaths paths, LlmOutputConfig output, DateTime nowUtc)
    {
        var week = ISOWeek.GetWeekOfYear(nowUtc);
        var relative = output.ProposalDirPattern
            .Replace("{iso_year}", nowUtc.Year.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{iso_week}", week.ToString("D2", CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace('/', Path.DirectorySeparatorChar);

        return Path.GetFullPath(Path.Combine(paths.Root, relative));
    }

    private static string ResolveCandidateFileName(string pattern, int index)
        => pattern.Replace("{n}", index.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static async Task WriteLlmLogAsync(
        ProjectPaths paths,
        LlmConfig config,
        string promptHash,
        LlmGenerationResult generation,
        IReadOnlyCollection<InputFile> inputs,
        IReadOnlyCollection<CandidateOutcome> outcomes,
        CancellationToken cancellationToken)
    {
        if (!config.Logging.Enabled)
        {
            return;
        }

        var logDir = ResolveLogDirectory(paths, config.Logging.LogDir);
        Directory.CreateDirectory(logDir);

        var now = DateTimeOffset.UtcNow;
        var logPath = Path.Combine(logDir, $"llm-run-{now:yyyyMMdd-HHmmss}.json");

        var node = new JsonObject
        {
            ["timestamp"] = now.ToString("O"),
            ["provider"] = generation.Provider,
            ["model"] = generation.Model,
            ["input_files"] = new JsonArray(inputs.Select(i => JsonValue.Create(i.Reference)).ToArray()),
            ["prompt_hash"] = config.Logging.IncludePromptHash ? promptHash : string.Empty,
            ["usage"] = generation.Usage is null
                ? null
                : new JsonObject
                {
                    ["prompt_tokens"] = generation.Usage.PromptTokens,
                    ["output_tokens"] = generation.Usage.OutputTokens,
                    ["total_tokens"] = generation.Usage.TotalTokens,
                },
            ["candidates"] = new JsonArray(outcomes.Select(outcome => new JsonObject
            {
                ["index"] = outcome.Index,
                ["name"] = outcome.Name,
                ["file"] = outcome.FileName,
                ["valid"] = outcome.IsValid,
                ["story_count"] = outcome.StoryCount,
                ["story_points"] = outcome.StoryPoints,
                ["validation_issues"] = new JsonArray(outcome.ValidationIssues.Select(i => JsonValue.Create(i.ToString())).ToArray()),
            }).ToArray()),
        };

        if (config.Logging.PersistRawLlmResponse)
        {
            node["raw_response"] = generation.RawText;
        }

        await File.WriteAllTextAsync(logPath, node.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
        }), cancellationToken);
    }

    private static string ResolveLogDirectory(ProjectPaths paths, string configuredDirectory)
    {
        if (Path.IsPathRooted(configuredDirectory))
        {
            return configuredDirectory;
        }

        return Path.GetFullPath(Path.Combine(paths.Root, configuredDirectory.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string BuildSummaryMarkdown(
        DateTime generatedAtUtc,
        IReadOnlyCollection<InputFile> inputs,
        IReadOnlyCollection<CandidateOutcome> outcomes)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# Candidate Comparison - {generatedAtUtc:yyyy}-W{ISOWeek.GetWeekOfYear(generatedAtUtc):D2}");
        builder.AppendLine();
        builder.AppendLine("## Inputs");
        foreach (var input in inputs)
        {
            builder.AppendLine($"- {input.Reference}");
        }

        builder.AppendLine();
        builder.AppendLine("## Scorecard");
        builder.AppendLine("| Candidate | Name | Strategic Fit | Delivery Risk | Dependency Risk | Effort Realism | Stories | Story Points | Validation |");
        builder.AppendLine("|---|---|---:|---:|---:|---:|---:|---:|---|");

        foreach (var candidate in outcomes.OrderBy(c => c.Index))
        {
            var score = candidate.Scores;
            builder.AppendLine(
                $"| {candidate.Index} | {EscapePipe(candidate.Name)} | {Format(score.StrategicAlignment)} | {Format(score.DeliveryRisk)} | {Format(score.DependencyRisk)} | {Format(score.EffortRealism)} | {candidate.StoryCount} | {candidate.StoryPoints} | {(candidate.IsValid ? "PASS" : "FAIL")} |");
        }

        foreach (var candidate in outcomes.OrderBy(c => c.Index))
        {
            builder.AppendLine();
            builder.AppendLine($"## Candidate {candidate.Index} ({candidate.Name})");
            builder.AppendLine($"- File: `{candidate.FileName}`");
            builder.AppendLine($"- Summary: {candidate.StrategySummary}");
            if (!string.IsNullOrWhiteSpace(candidate.Rationale))
            {
                builder.AppendLine($"- Rationale: {candidate.Rationale}");
            }

            if (candidate.IsValid)
            {
                builder.AppendLine("- Validation: PASS");
            }
            else
            {
                builder.AppendLine("- Validation: FAIL");
                foreach (var issue in candidate.ValidationIssues)
                {
                    builder.AppendLine($"  - {issue}");
                }
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Human Decision");
        builder.AppendLine("- Selected candidate: `__`");
        builder.AppendLine("- Rationale: `__`");
        builder.AppendLine("- Next command: `cto-engine plan --select <n> --project <project-root>`");
        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    private static string EscapePipe(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);

    private static string Format(double? value)
        => value.HasValue ? value.Value.ToString("0.0", CultureInfo.InvariantCulture) : "-";

    private static string EnsureTrailingNewLine(string value)
        => value.EndsWith(Environment.NewLine, StringComparison.Ordinal) ? value : value + Environment.NewLine;

    private static string Clip(string content, int maxChars)
    {
        if (content.Length <= maxChars)
        {
            return content;
        }

        var clipped = content[..Math.Max(0, maxChars - 32)];
        return clipped + "\n\n[...truncated for token budget...]";
    }

    private static int EstimateTokens(string text)
        => (int)Math.Ceiling(text.Length / 4.0);

    private static string ComputeSha256(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private sealed record InputFile(string Reference, string Path, bool Required);

    private sealed class CandidateOutcome
    {
        public required int Index { get; init; }
        public required string Name { get; init; }
        public required string StrategySummary { get; init; }
        public required string FileName { get; init; }
        public required string FilePath { get; init; }
        public required bool IsValid { get; init; }
        public required IReadOnlyCollection<ValidationIssue> ValidationIssues { get; init; }
        public required int StoryCount { get; init; }
        public required int StoryPoints { get; init; }
        public string? Rationale { get; init; }
        public ProposedPlanScores Scores { get; init; } = new();
    }
}
