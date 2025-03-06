using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ResearchAssistant.Core.Models;

namespace ResearchAssistant.Core.Interfaces;

public interface IPlanner
{
    Task<ReportState> GenerateReportPlanAsync(
        ReportState state,
        CancellationToken cancellationToken = default
    );
}
