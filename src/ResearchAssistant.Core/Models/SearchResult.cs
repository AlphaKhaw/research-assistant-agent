namespace ResearchAssistant.Core.Models;

public class SearchResult
{
    public string Title { get; set; } = string.Empty; // Title of search result
    public string Url { get; set; } = string.Empty; // URL of the source
    public string Snippet { get; set; } = string.Empty; // Brief excerpt from the source
    public string Content { get; set; } = string.Empty; // Full content if retrieved
}
