namespace ResearchAssistant.Core.Models;

public class Section
{
    public string Name { get; set; } = string.Empty; // Name of the report section
    public string Description { get; set; } = string.Empty; // Brief description of what section contains
    public bool Research { get; set; } // Whether this section needs research
    public string Content { get; set; } = string.Empty; // The actual content/text of the section
}
