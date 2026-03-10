using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cto.Core.Common;

namespace Cto.Core.LLM;

public sealed class GeminiClient : ILlmClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _disposeClient;

    public GeminiClient(HttpClient? httpClient = null)
    {
        if (httpClient is not null)
        {
            _httpClient = httpClient;
            _disposeClient = false;
            return;
        }

        _httpClient = new HttpClient();
        _disposeClient = true;
    }

    public async Task<LlmGenerationResult> GeneratePlansAsync(
        string prompt,
        GeminiProviderConfig provider,
        LlmGenerationConfig generation,
        CancellationToken cancellationToken = default)
    {
        var apiKey = Environment.GetEnvironmentVariable(provider.ApiKeyEnvVar);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                $"Missing Gemini credentials. Set environment variable '{provider.ApiKeyEnvVar}'.");
        }

        var baseUrl = provider.BaseUrl.TrimEnd('/');
        var model = provider.Model.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException("Gemini provider configuration is invalid (base_url/model).");
        }

        var endpoint = $"{baseUrl}/v1beta/models/{Uri.EscapeDataString(model)}:generateContent";
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("x-goog-api-key", apiKey);

        var maxOutputTokens = Math.Max(512, generation.MaxOutputTokensPerCandidate * Math.Max(1, generation.Candidates));
        var payload = new JsonObject
        {
            ["contents"] = new JsonArray(
                new JsonObject
                {
                    ["role"] = "user",
                    ["parts"] = new JsonArray(
                        new JsonObject
                        {
                            ["text"] = prompt,
                        }),
                }),
            ["generationConfig"] = new JsonObject
            {
                ["temperature"] = generation.Temperature,
                ["topP"] = generation.TopP,
                ["maxOutputTokens"] = maxOutputTokens,
            },
        };

        request.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // Never include secrets in thrown errors. We keep body small for diagnostics.
            var diagnosticBody = responseBody.Length > 1200
                ? responseBody[..1200] + "..."
                : responseBody;
            throw new InvalidOperationException(
                $"Gemini API request failed ({(int)response.StatusCode} {response.ReasonPhrase}): {diagnosticBody}");
        }

        var root = JsonNode.Parse(responseBody) as JsonObject
                   ?? throw new InvalidOperationException("Gemini response is not valid JSON.");

        var text = ExtractText(root);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Gemini response did not include any text content.");
        }

        var usage = ExtractUsage(root);
        return new LlmGenerationResult
        {
            Provider = "gemini",
            Model = model,
            RawText = text,
            Usage = usage,
        };
    }

    private static string ExtractText(JsonObject root)
    {
        if (root["candidates"] is not JsonArray candidates)
        {
            return string.Empty;
        }

        foreach (var candidateNode in candidates)
        {
            if (candidateNode is not JsonObject candidate ||
                candidate["content"] is not JsonObject content ||
                content["parts"] is not JsonArray parts)
            {
                continue;
            }

            var builder = new StringBuilder();
            foreach (var partNode in parts)
            {
                if (partNode is not JsonObject part)
                {
                    continue;
                }

                var text = part["text"]?.GetValue<string?>();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    builder.AppendLine(text.Trim());
                }
            }

            var combined = builder.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(combined))
            {
                return combined;
            }
        }

        return string.Empty;
    }

    private static LlmUsage? ExtractUsage(JsonObject root)
    {
        if (root["usageMetadata"] is not JsonObject usageNode)
        {
            return null;
        }

        return new LlmUsage
        {
            PromptTokens = usageNode["promptTokenCount"]?.GetValue<int?>() ?? 0,
            OutputTokens = usageNode["candidatesTokenCount"]?.GetValue<int?>() ?? 0,
            TotalTokens = usageNode["totalTokenCount"]?.GetValue<int?>() ?? 0,
        };
    }

    public void Dispose()
    {
        if (_disposeClient)
        {
            _httpClient.Dispose();
        }
    }
}
