namespace ResearchAssistant.Core.Models;

public class SearchOptions
{
    public int MaxResults { get; set; } = 5; // Maximum number of results to return
    public bool IncludeUrls { get; set; } = true; // Whether to include URLs in results
    public bool IncludeSnippets { get; set; } = true; // Whether to include snippets
    public bool IncludeContent { get; set; } = false; // Whether to include full content
}
