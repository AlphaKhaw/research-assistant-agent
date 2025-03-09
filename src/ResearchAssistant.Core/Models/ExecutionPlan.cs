namespace ResearchAssistant.Core.Models;

public class ExecutionPlan
{
    public string PlanId { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public ReportPlan ApprovedPlan { get; set; } = new();
    public List<SectionResearchTask> ResearchTasks { get; set; } = new();
    public int MaxConcurrentSections { get; set; }
    public ExecutionStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class SectionResearchTask
{
    public string SectionId { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TaskStatus Status { get; set; }
    public int MaxSearchQueries { get; set; }
    public List<SearchResultSet> SearchResults { get; set; } = new();
    public string Content { get; set; } = string.Empty;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class SearchResultSet
{
    public string Query { get; set; } = string.Empty;
    public List<SearchResult> Results { get; set; } = new();
    public DateTime Timestamp { get; set; }
}

public enum ExecutionStatus
{
    Ready,
    InProgress,
    Paused,
    Completed,
    Failed,
}

public enum TaskStatus
{
    Pending,
    InProgress,
    Paused,
    Completed,
    Failed,
}
