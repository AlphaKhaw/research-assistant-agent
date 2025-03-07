using System.Collections.Generic;

namespace ResearchAssistant.Core.Models;

public class FunctionCallInfo
{
    // Name of the function called
    public string Name { get; set; } = string.Empty;

    // Parameters passed to the function
    public Dictionary<string, object> Parameters { get; set; } = new();

    // Result of the function call
    public object? Result { get; set; }
}
