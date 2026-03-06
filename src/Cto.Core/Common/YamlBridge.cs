using System.Text.Json;
using System.Text.Json.Nodes;

namespace Cto.Core.Common;

public static class YamlBridge
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static async Task<(JsonNode? Json, string? Error)> LoadAsJsonAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return (null, $"YAML file not found: {filePath}");
        }

        var rubyScript =
            "require 'yaml'; require 'json'; require 'date'; " +
            "input = File.read(ARGV[0]); " +
            "data = YAML.safe_load(input, permitted_classes: [Date, Time], aliases: true); " +
            "puts JSON.generate(data.nil? ? {} : data)";

        var result = await ProcessRunner.RunAsync(
            "ruby",
            ["-ryaml", "-rjson", "-e", rubyScript, filePath],
            cancellationToken: cancellationToken);

        if (!result.Success)
        {
            var error = string.IsNullOrWhiteSpace(result.StdErr)
                ? result.StdOut
                : result.StdErr;

            return (null, $"Failed to parse YAML '{filePath}' using Ruby bridge: {error.Trim()}");
        }

        try
        {
            var node = JsonNode.Parse(result.StdOut);
            return (node, null);
        }
        catch (Exception ex)
        {
            return (null, $"Failed to parse JSON converted from YAML '{filePath}': {ex.Message}");
        }
    }

    public static async Task<(T? Value, string? Error)> LoadAsAsync<T>(
        string filePath,
        CancellationToken cancellationToken = default)
        where T : class
    {
        var (json, error) = await LoadAsJsonAsync(filePath, cancellationToken);
        if (error is not null)
        {
            return (default, error);
        }

        try
        {
            var model = json is null ? null : json.Deserialize<T>(SerializerOptions);
            return (model, null);
        }
        catch (Exception ex)
        {
            return (default, $"Failed to deserialize YAML '{filePath}' to {typeof(T).Name}: {ex.Message}");
        }
    }
}
