namespace ResearchAssistant.Core.Models;

public class ExecutionOptions
{
    public int MaxConcurrentSections { get; set; } = 3;
    public int MaxSearchQueriesPerSection { get; set; } = 3;
    public bool IncludeReflection { get; set; } = true;
    public bool PauseAfterEachSection { get; set; } = false;
}
