using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ResearchAssistant.Core.Models;

namespace ResearchAssistant.Core.Interfaces;

public interface ISectionWriter
{
    Task<Report> WriteReportAsync(
        ExecutionPlan plan,
        CancellationToken cancellationToken = default
    );
}
