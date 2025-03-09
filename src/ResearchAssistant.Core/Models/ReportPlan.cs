namespace ResearchAssistant.Core.Models;

public class ReportPlan
{
    public string Id { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string Organization { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<ReportSection> Sections { get; set; } = new();
    public List<PlanRevision> RevisionHistory { get; set; } = new();
    public int TokensUsed { get; set; }
}

public class ReportSection
{
    public string Id { get; set; } = string.Empty;
    public int Number { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool RequiresResearch { get; set; }
    public string Content { get; set; } = string.Empty;
    public ExecutionPhase ExecutionPhase { get; set; }
}

public class PlanRevision
{
    public DateTime Timestamp { get; set; }
    public string Feedback { get; set; } = string.Empty;
    public List<ReportSection> PreviousSections { get; set; } = new();
}
