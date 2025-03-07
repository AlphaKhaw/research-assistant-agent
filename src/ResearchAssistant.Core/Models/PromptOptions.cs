using System.Collections.Generic;

namespace ResearchAssistant.Core.Models;

public class PromptOptions
{
    // Temperature for generation (null uses default)
    public float? Temperature { get; set; }

    // Top-p sampling value (null uses default)
    public float? TopP { get; set; }

    // Maximum tokens to generate (null uses default)
    public int? MaxTokens { get; set; }

    // Whether to include citations in responses
    public bool IncludeCitations { get; set; } = true;

    // Search options for research tasks
    public SearchOptions SearchOptions { get; set; } = new SearchOptions();

    // Additional context data to pass to the prompt
    public Dictionary<string, object> ContextData { get; set; } = new Dictionary<string, object>();
}
