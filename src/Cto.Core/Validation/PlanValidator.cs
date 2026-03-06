using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Cto.Core.Common;

namespace Cto.Core.Validation;

public sealed class PlanValidator
{
    private static readonly HashSet<int> FibonacciEstimates = [1, 2, 3, 5, 8, 13];
    private static readonly HashSet<string> TopLevelFields =
    [
        "version", "goal", "success_criteria", "risks", "work_breakdown", "assumptions", "dependencies", "metadata",
    ];

    public async Task<ValidationResult> ValidateAsync(string planPath, CancellationToken cancellationToken = default)
    {
        var result = new ValidationResult();
        if (!File.Exists(planPath))
        {
            result.AddError("PLAN_MISSING", "plan.yaml is missing.", planPath);
            return result;
        }

        var (node, error) = await YamlBridge.LoadAsJsonAsync(planPath, cancellationToken);
        if (error is not null)
        {
            result.AddError("PLAN_PARSE_ERROR", error, planPath);
            return result;
        }

        var root = AsObject(node, "$", result);
        if (root is null)
        {
            return result;
        }

        ValidateAllowedFields(root, TopLevelFields, "$", result);
        ValidateRequired(root, ["version", "goal", "success_criteria", "risks", "work_breakdown", "assumptions"], "$", result);

        ValidateVersion(root, result);
        ValidateGoal(root, result);
        ValidateSuccessCriteria(root, result);
        ValidateRisks(root, result);
        ValidateWorkBreakdown(root, result);
        ValidateAssumptions(root, result);
        ValidateDependencies(root, result);
        ValidateMetadata(root, result);

        return result;
    }

    private static void ValidateVersion(JsonObject root, ValidationResult result)
    {
        var version = GetRequiredString(root, "version", "$.version", result, minLength: 3);
        if (version is not null && !Regex.IsMatch(version, @"^\d+\.\d+$"))
        {
            result.AddError("PLAN_VERSION_INVALID", "version must match '<major>.<minor>' (e.g., 1.0).", "$.version");
        }
    }

    private static void ValidateGoal(JsonObject root, ValidationResult result)
    {
        _ = GetRequiredString(root, "goal", "$.goal", result, minLength: 10, maxLength: 200);
    }

    private static void ValidateSuccessCriteria(JsonObject root, ValidationResult result)
    {
        var criteria = AsArray(root["success_criteria"], "$.success_criteria", result);
        if (criteria is null)
        {
            return;
        }

        ValidateArrayBounds(criteria, "$.success_criteria", result, minItems: 1, maxItems: 5);
        for (var i = 0; i < criteria.Count; i++)
        {
            var path = $"$.success_criteria[{i}]";
            if (criteria[i]?.GetValue<string?>() is not { } value)
            {
                result.AddError("PLAN_SUCCESS_CRITERIA_TYPE", "Success criterion must be a string.", path);
                continue;
            }

            if (value.Trim().Length < 10)
            {
                result.AddError("PLAN_SUCCESS_CRITERIA_SHORT", "Success criterion must have at least 10 characters.", path);
            }
        }
    }

    private static void ValidateRisks(JsonObject root, ValidationResult result)
    {
        var risks = AsArray(root["risks"], "$.risks", result);
        if (risks is null)
        {
            return;
        }

        for (var i = 0; i < risks.Count; i++)
        {
            var path = $"$.risks[{i}]";
            var risk = AsObject(risks[i], path, result);
            if (risk is null)
            {
                continue;
            }

            ValidateAllowedFields(risk, ["risk", "mitigation", "severity", "ai_warned"], path, result);
            ValidateRequired(risk, ["risk", "mitigation", "severity"], path, result);
            _ = GetRequiredString(risk, "risk", path + ".risk", result, minLength: 10);
            _ = GetRequiredString(risk, "mitigation", path + ".mitigation", result, minLength: 10);

            var severity = GetRequiredString(risk, "severity", path + ".severity", result);
            if (severity is not null && severity is not ("low" or "medium" or "high" or "critical"))
            {
                result.AddError("PLAN_RISK_SEVERITY", "severity must be one of: low, medium, high, critical.", path + ".severity");
            }

            if (risk.ContainsKey("ai_warned") && risk["ai_warned"] is not null && risk["ai_warned"]!.GetValueKind() is not System.Text.Json.JsonValueKind.True and not System.Text.Json.JsonValueKind.False)
            {
                result.AddError("PLAN_RISK_AI_WARNED", "ai_warned must be a boolean.", path + ".ai_warned");
            }
        }
    }

    private static void ValidateWorkBreakdown(JsonObject root, ValidationResult result)
    {
        var workBreakdown = AsArray(root["work_breakdown"], "$.work_breakdown", result);
        if (workBreakdown is null)
        {
            return;
        }

        ValidateArrayBounds(workBreakdown, "$.work_breakdown", result, minItems: 1);

        for (var epicIndex = 0; epicIndex < workBreakdown.Count; epicIndex++)
        {
            var epicPath = $"$.work_breakdown[{epicIndex}]";
            var epic = AsObject(workBreakdown[epicIndex], epicPath, result);
            if (epic is null)
            {
                continue;
            }

            ValidateAllowedFields(epic, ["epic_key", "summary", "description", "stories"], epicPath, result);
            ValidateRequired(epic, ["epic_key", "summary", "stories"], epicPath, result);

            var epicKey = GetRequiredString(epic, "epic_key", epicPath + ".epic_key", result);
            if (epicKey is not null && !Regex.IsMatch(epicKey, @"^([A-Z]+-\d+)?$"))
            {
                result.AddError("PLAN_EPIC_KEY", "epic_key must be empty or Jira key format (ABC-123).", epicPath + ".epic_key");
            }

            _ = GetRequiredString(epic, "summary", epicPath + ".summary", result, minLength: 10, maxLength: 150);
            if (epic.ContainsKey("description") && epic["description"] is not null && epic["description"]!.GetValue<string?>() is null)
            {
                result.AddError("PLAN_EPIC_DESCRIPTION_TYPE", "description must be a string.", epicPath + ".description");
            }

            var stories = AsArray(epic["stories"], epicPath + ".stories", result);
            if (stories is null)
            {
                continue;
            }

            ValidateArrayBounds(stories, epicPath + ".stories", result, minItems: 1);

            for (var storyIndex = 0; storyIndex < stories.Count; storyIndex++)
            {
                var storyPath = $"{epicPath}.stories[{storyIndex}]";
                var story = AsObject(stories[storyIndex], storyPath, result);
                if (story is null)
                {
                    continue;
                }

                ValidateAllowedFields(story,
                [
                    "story_key", "summary", "objective", "scope", "acceptance_criteria", "references", "constraints",
                    "definition_of_done", "estimate", "priority", "labels", "dependencies",
                ],
                storyPath,
                result);

                ValidateRequired(story,
                ["summary", "objective", "scope", "acceptance_criteria", "definition_of_done", "estimate"],
                storyPath,
                result);

                var storyKey = GetOptionalString(story, "story_key", storyPath + ".story_key", result);
                if (storyKey is not null && !Regex.IsMatch(storyKey, @"^([A-Z]+-\d+)?$"))
                {
                    result.AddError("PLAN_STORY_KEY", "story_key must be empty or Jira key format (ABC-123).", storyPath + ".story_key");
                }

                _ = GetRequiredString(story, "summary", storyPath + ".summary", result, minLength: 10, maxLength: 150);
                _ = GetRequiredString(story, "objective", storyPath + ".objective", result, minLength: 20);

                ValidateScope(story, storyPath, result);
                ValidateStringArray(story, "acceptance_criteria", storyPath + ".acceptance_criteria", result, minItems: 1, minLengthPerItem: 10);
                ValidateReferences(story, storyPath, result);
                ValidateStringArray(story, "constraints", storyPath + ".constraints", result, minItems: 0, minLengthPerItem: 10);
                ValidateStringArray(story, "definition_of_done", storyPath + ".definition_of_done", result, minItems: 1, minLengthPerItem: 5);
                ValidateEstimate(story, storyPath, result);
                ValidatePriority(story, storyPath, result);
                ValidateLabels(story, storyPath, result);
                ValidateStoryDependencies(story, storyPath, result);
            }
        }
    }

    private static void ValidateAssumptions(JsonObject root, ValidationResult result)
        => ValidateStringArray(root, "assumptions", "$.assumptions", result, minItems: 0, minLengthPerItem: 10);

    private static void ValidateDependencies(JsonObject root, ValidationResult result)
    {
        if (!root.TryGetPropertyValue("dependencies", out var dependenciesNode) || dependenciesNode is null)
        {
            return;
        }

        var dependencies = AsArray(dependenciesNode, "$.dependencies", result);
        if (dependencies is null)
        {
            return;
        }

        for (var i = 0; i < dependencies.Count; i++)
        {
            var path = $"$.dependencies[{i}]";
            var dependency = AsObject(dependencies[i], path, result);
            if (dependency is null)
            {
                continue;
            }

            ValidateAllowedFields(dependency, ["description", "type", "blocker"], path, result);
            ValidateRequired(dependency, ["description", "type"], path, result);

            _ = GetRequiredString(dependency, "description", path + ".description", result, minLength: 10);
            var type = GetRequiredString(dependency, "type", path + ".type", result);
            if (type is not null && type is not ("external_api" or "third_party" or "infrastructure" or "team" or "other"))
            {
                result.AddError("PLAN_DEPENDENCY_TYPE", "type must be one of: external_api, third_party, infrastructure, team, other.", path + ".type");
            }

            if (dependency.ContainsKey("blocker") && dependency["blocker"] is not null && dependency["blocker"]!.GetValueKind() is not System.Text.Json.JsonValueKind.True and not System.Text.Json.JsonValueKind.False)
            {
                result.AddError("PLAN_DEPENDENCY_BLOCKER", "blocker must be a boolean.", path + ".blocker");
            }
        }
    }

    private static void ValidateMetadata(JsonObject root, ValidationResult result)
    {
        if (!root.TryGetPropertyValue("metadata", out var metadataNode) || metadataNode is null)
        {
            return;
        }

        var metadata = AsObject(metadataNode, "$.metadata", result);
        if (metadata is null)
        {
            return;
        }

        ValidateAllowedFields(metadata,
            ["created_at", "approved_at", "approved_by", "commit_sha", "week_number", "ai_proposal_followed"],
            "$.metadata",
            result);

        ValidateDateTime(metadata, "created_at", "$.metadata.created_at", result);
        ValidateDateTime(metadata, "approved_at", "$.metadata.approved_at", result);
        _ = GetOptionalString(metadata, "approved_by", "$.metadata.approved_by", result);

        var commitSha = GetOptionalString(metadata, "commit_sha", "$.metadata.commit_sha", result);
        if (commitSha is not null && !Regex.IsMatch(commitSha, @"^[a-f0-9]{40}$"))
        {
            result.AddError("PLAN_METADATA_COMMIT_SHA", "metadata.commit_sha must be a full 40-character lowercase SHA-1 hash.", "$.metadata.commit_sha");
        }

        if (metadata.TryGetPropertyValue("week_number", out var weekNode) && weekNode is not null && GetInteger(weekNode) is null)
        {
            result.AddError("PLAN_METADATA_WEEK", "metadata.week_number must be an integer.", "$.metadata.week_number");
        }

        if (metadata.TryGetPropertyValue("ai_proposal_followed", out var followedNode) && followedNode is not null && followedNode.GetValueKind() is not System.Text.Json.JsonValueKind.True and not System.Text.Json.JsonValueKind.False)
        {
            result.AddError("PLAN_METADATA_AI_PROPOSAL", "metadata.ai_proposal_followed must be a boolean.", "$.metadata.ai_proposal_followed");
        }
    }

    private static void ValidateScope(JsonObject story, string storyPath, ValidationResult result)
    {
        var scope = AsObject(story["scope"], storyPath + ".scope", result);
        if (scope is null)
        {
            return;
        }

        ValidateAllowedFields(scope, ["in_scope", "out_of_scope"], storyPath + ".scope", result);
        ValidateRequired(scope, ["in_scope", "out_of_scope"], storyPath + ".scope", result);

        ValidateStringArray(scope, "in_scope", storyPath + ".scope.in_scope", result, minItems: 1, minLengthPerItem: 5);
        ValidateStringArray(scope, "out_of_scope", storyPath + ".scope.out_of_scope", result, minItems: 1, minLengthPerItem: 5);
    }

    private static void ValidateReferences(JsonObject story, string storyPath, ValidationResult result)
    {
        if (!story.TryGetPropertyValue("references", out var referencesNode) || referencesNode is null)
        {
            return;
        }

        var references = AsArray(referencesNode, storyPath + ".references", result);
        if (references is null)
        {
            return;
        }

        for (var i = 0; i < references.Count; i++)
        {
            var refPath = $"{storyPath}.references[{i}]";
            var reference = AsObject(references[i], refPath, result);
            if (reference is null)
            {
                continue;
            }

            ValidateAllowedFields(reference, ["title", "url"], refPath, result);
            ValidateRequired(reference, ["title", "url"], refPath, result);
            _ = GetRequiredString(reference, "title", refPath + ".title", result, minLength: 1);
            _ = GetRequiredString(reference, "url", refPath + ".url", result, minLength: 1);
        }
    }

    private static void ValidateEstimate(JsonObject story, string storyPath, ValidationResult result)
    {
        if (!story.TryGetPropertyValue("estimate", out var estimateNode) || estimateNode is null)
        {
            result.AddError("PLAN_STORY_ESTIMATE_REQUIRED", "estimate is required.", storyPath + ".estimate");
            return;
        }

        if (GetInteger(estimateNode) is not { } estimate)
        {
            result.AddError("PLAN_STORY_ESTIMATE_TYPE", "estimate must be an integer.", storyPath + ".estimate");
            return;
        }

        if (!FibonacciEstimates.Contains(estimate))
        {
            result.AddError("PLAN_STORY_ESTIMATE_FIBONACCI", "estimate must be one of: 1, 2, 3, 5, 8, 13.", storyPath + ".estimate");
        }
    }

    private static void ValidatePriority(JsonObject story, string storyPath, ValidationResult result)
    {
        var priority = GetOptionalString(story, "priority", storyPath + ".priority", result);
        if (priority is null)
        {
            return;
        }

        if (priority is not ("lowest" or "low" or "medium" or "high" or "highest"))
        {
            result.AddError("PLAN_STORY_PRIORITY", "priority must be one of: lowest, low, medium, high, highest.", storyPath + ".priority");
        }
    }

    private static void ValidateLabels(JsonObject story, string storyPath, ValidationResult result)
    {
        if (!story.TryGetPropertyValue("labels", out var labelsNode) || labelsNode is null)
        {
            return;
        }

        var labels = AsArray(labelsNode, storyPath + ".labels", result);
        if (labels is null)
        {
            return;
        }

        for (var i = 0; i < labels.Count; i++)
        {
            if (labels[i]?.GetValue<string?>() is null)
            {
                result.AddError("PLAN_STORY_LABEL_TYPE", "labels entries must be strings.", $"{storyPath}.labels[{i}]");
            }
        }
    }

    private static void ValidateStoryDependencies(JsonObject story, string storyPath, ValidationResult result)
    {
        if (!story.TryGetPropertyValue("dependencies", out var dependenciesNode) || dependenciesNode is null)
        {
            return;
        }

        var dependencies = AsArray(dependenciesNode, storyPath + ".dependencies", result);
        if (dependencies is null)
        {
            return;
        }

        for (var i = 0; i < dependencies.Count; i++)
        {
            var path = $"{storyPath}.dependencies[{i}]";
            var dependency = dependencies[i]?.GetValue<string?>();
            if (dependency is null)
            {
                result.AddError("PLAN_STORY_DEPENDENCY_TYPE", "Story dependency must be a string.", path);
                continue;
            }

            if (!Regex.IsMatch(dependency, @"^[A-Z]+-\d+$"))
            {
                result.AddError("PLAN_STORY_DEPENDENCY_FORMAT", "Story dependency must match Jira key format (ABC-123).", path);
            }
        }
    }

    private static void ValidateDateTime(JsonObject objectNode, string propertyName, string path, ValidationResult result)
    {
        var value = GetOptionalString(objectNode, propertyName, path, result);
        if (value is null)
        {
            return;
        }

        if (!DateTimeOffset.TryParse(value, out _))
        {
            result.AddError("PLAN_DATETIME_FORMAT", "Expected RFC3339/ISO date-time format.", path);
        }
    }

    private static void ValidateStringArray(
        JsonObject objectNode,
        string propertyName,
        string path,
        ValidationResult result,
        int minItems,
        int minLengthPerItem)
    {
        if (!objectNode.TryGetPropertyValue(propertyName, out var node) || node is null)
        {
            if (minItems > 0)
            {
                result.AddError("PLAN_ARRAY_REQUIRED", $"{propertyName} is required.", path);
            }

            return;
        }

        var array = AsArray(node, path, result);
        if (array is null)
        {
            return;
        }

        ValidateArrayBounds(array, path, result, minItems);
        for (var i = 0; i < array.Count; i++)
        {
            var value = array[i]?.GetValue<string?>();
            if (value is null)
            {
                result.AddError("PLAN_ARRAY_ITEM_TYPE", "Array entry must be a string.", $"{path}[{i}]");
                continue;
            }

            if (value.Trim().Length < minLengthPerItem)
            {
                result.AddError("PLAN_ARRAY_ITEM_LENGTH", $"Array entry must have at least {minLengthPerItem} characters.", $"{path}[{i}]");
            }
        }
    }

    private static void ValidateArrayBounds(JsonArray array, string path, ValidationResult result, int minItems, int? maxItems = null)
    {
        if (array.Count < minItems)
        {
            result.AddError("PLAN_ARRAY_MIN_ITEMS", $"Expected at least {minItems} item(s).", path);
        }

        if (maxItems.HasValue && array.Count > maxItems.Value)
        {
            result.AddError("PLAN_ARRAY_MAX_ITEMS", $"Expected at most {maxItems.Value} item(s).", path);
        }
    }

    private static void ValidateAllowedFields(JsonObject objectNode, IEnumerable<string> allowedFields, string path, ValidationResult result)
    {
        var allowed = new HashSet<string>(allowedFields, StringComparer.Ordinal);
        foreach (var property in objectNode)
        {
            if (!allowed.Contains(property.Key))
            {
                result.AddError("PLAN_UNKNOWN_FIELD", $"Unknown field '{property.Key}'. Strict schema forbids extra fields.", path + "." + property.Key);
            }
        }
    }

    private static void ValidateRequired(JsonObject objectNode, IEnumerable<string> requiredFields, string path, ValidationResult result)
    {
        foreach (var required in requiredFields)
        {
            if (!objectNode.ContainsKey(required) || objectNode[required] is null)
            {
                result.AddError("PLAN_REQUIRED_FIELD", $"Missing required field '{required}'.", path + "." + required);
            }
        }
    }

    private static string? GetRequiredString(JsonObject objectNode, string propertyName, string path, ValidationResult result, int minLength = 0, int? maxLength = null)
    {
        var value = GetOptionalString(objectNode, propertyName, path, result);
        if (value is null)
        {
            result.AddError("PLAN_STRING_REQUIRED", $"{propertyName} is required and must be a string.", path);
            return null;
        }

        if (value.Trim().Length < minLength)
        {
            result.AddError("PLAN_STRING_MIN", $"{propertyName} must have at least {minLength} characters.", path);
        }

        if (maxLength.HasValue && value.Trim().Length > maxLength.Value)
        {
            result.AddError("PLAN_STRING_MAX", $"{propertyName} must have at most {maxLength.Value} characters.", path);
        }

        return value;
    }

    private static string? GetOptionalString(JsonObject objectNode, string propertyName, string path, ValidationResult result)
    {
        if (!objectNode.TryGetPropertyValue(propertyName, out var node) || node is null)
        {
            return null;
        }

        string? value;
        try
        {
            value = node.GetValue<string?>();
        }
        catch
        {
            value = null;
        }

        if (value is null)
        {
            result.AddError("PLAN_STRING_TYPE", $"{propertyName} must be a string.", path);
        }

        return value;
    }

    private static JsonObject? AsObject(JsonNode? node, string path, ValidationResult result)
    {
        if (node is JsonObject jsonObject)
        {
            return jsonObject;
        }

        result.AddError("PLAN_OBJECT_TYPE", "Expected object value.", path);
        return null;
    }

    private static JsonArray? AsArray(JsonNode? node, string path, ValidationResult result)
    {
        if (node is JsonArray jsonArray)
        {
            return jsonArray;
        }

        result.AddError("PLAN_ARRAY_TYPE", "Expected array value.", path);
        return null;
    }

    private static int? GetInteger(JsonNode node)
    {
        try
        {
            return node.GetValue<int>();
        }
        catch
        {
            return null;
        }
    }
}
