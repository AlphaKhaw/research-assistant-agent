using System;
using System.Collections.Generic;

namespace ResearchAssistant.Core.Models;

public class Report
{
    public string Id { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public List<ReportContent> Sections { get; set; } = new();
    public List<Citation> Citations { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public string PlanId { get; set; } = string.Empty;
    public int TokensUsed { get; set; }
}

public class ReportContent
{
    public string SectionId { get; set; } = string.Empty;
    public int SectionNumber { get; set; }
    public string SectionName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsRevised { get; set; }
}

public class Citation
{
    public string Id { get; set; } = string.Empty;
    public int Number { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}
