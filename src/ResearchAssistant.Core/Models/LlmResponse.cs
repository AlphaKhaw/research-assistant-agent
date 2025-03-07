using System.Collections.Generic;

namespace ResearchAssistant.Core.Models;

public class LlmResponse
{
    // Generated content
    public string Content { get; set; } = string.Empty;

    // Token count used in the request and response
    public int TokensUsed { get; set; }

    // Function calls made during generation
    public List<FunctionCallInfo> FunctionCalls { get; set; } = new();
}
