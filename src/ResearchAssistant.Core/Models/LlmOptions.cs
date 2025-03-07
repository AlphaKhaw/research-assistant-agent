namespace ResearchAssistant.Core.Models;

public class LlmOptions
{
    // OpenAI API key
    public string ApiKey { get; set; } = string.Empty;

    // Model ID to use (e.g., "gpt-4o" or "gpt-4-turbo")
    public string ModelId { get; set; } = "gpt-4o";

    // Default temperature for generations
    public float DefaultTemperature { get; set; } = 0.7f;

    // Default top-p value for generations
    public float DefaultTopP { get; set; } = 1.0f;

    // Default maximum tokens to generate
    public int DefaultMaxTokens { get; set; } = 4096;

    // Whether to enable function calling by default
    public bool EnableFunctionCalling { get; set; } = true;
}
