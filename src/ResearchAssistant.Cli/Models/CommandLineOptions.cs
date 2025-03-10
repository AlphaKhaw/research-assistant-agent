namespace ResearchAssistant.Cli.Models;

/// <summary>
/// Represents options that can be specified via command-line arguments.
/// </summary>
public class CommandLineOptions
{
    /// <summary>The research topic to investigate</summary>
    public string Topic { get; set; } = "";

    /// <summary>Specific organization structure for the report</summary>
    public string Organization { get; set; } = "";

    /// <summary>Additional context for the research</summary>
    public string Context { get; set; } = "";

    /// <summary>Whether to use web search capability (true by default)</summary>
    public bool UseSearch { get; set; } = true;

    /// <summary>Model temperature value between 0.0-1.0 (lower is more deterministic)</summary>
    public float Temperature { get; set; } = 0.2f;

    /// <summary>Maximum number of sections to process concurrently</summary>
    public int MaxConcurrentSections { get; set; } = 2;

    /// <summary>Maximum number of search queries per section</summary>
    public int MaxSearchQueries { get; set; } = 3;

    /// <summary>Whether to display help information</summary>
    public bool ShowHelp { get; set; } = false;

    /// <summary>Directory to save the report (defaults to current directory)</summary>
    public string OutputDirectory { get; set; } = "";
}
