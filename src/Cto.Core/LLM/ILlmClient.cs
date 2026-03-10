using Cto.Core.Common;

namespace Cto.Core.LLM;

public interface ILlmClient
{
    Task<LlmGenerationResult> GeneratePlansAsync(
        string prompt,
        GeminiProviderConfig provider,
        LlmGenerationConfig generation,
        CancellationToken cancellationToken = default);
}
