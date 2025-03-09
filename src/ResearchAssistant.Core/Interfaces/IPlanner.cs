using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ResearchAssistant.Core.Models;

namespace ResearchAssistant.Core.Interfaces;

public interface IPlanner
{
    Task<ReportPlan> GenerateInitialPlanAsync(
        string topic,
        string organization = "Standard academic report",
        string context = "",
        PromptOptions? promptOptions = null,
        CancellationToken cancellationToken = default
    );

    Task<ReportPlan> ReviseWithFeedbackAsync(
        ReportPlan existingPlan,
        string feedback,
        PromptOptions? promptOptions = null,
        CancellationToken cancellationToken = default
    );

    Task<ExecutionPlan> PrepareForExecutionAsync(
        ReportPlan approvedPlan,
        ExecutionOptions? executionOptions = null,
        CancellationToken cancellationToken = default
    );
}
