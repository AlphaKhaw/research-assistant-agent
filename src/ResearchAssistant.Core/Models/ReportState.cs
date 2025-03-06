using System.Collections.Generic;

namespace ResearchAssistant.Core.Models;

public class ReportState
{
    public string Topic { get; set; } = string.Empty; // Main research topic
    public string FeedbackOnReportPlan { get; set; } = string.Empty; // User feedback
    public List<Section> Sections { get; set; } = new(); // All planned sections
    public List<Section> CompletedSections { get; set; } = new(); // Sections that are finished
    public string ReportSectionsFromResearch { get; set; } = string.Empty; // Research data
    public string FinalReport { get; set; } = string.Empty; // The complete final report
}
