using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ResearchAssistant.Core.Models;

namespace ResearchAssistant.Core.Interfaces;

public interface ISectionWriter
{
    Task<Section> WriteSectionAsync(
        Section section,
        ReportState reportState,
        List<SearchResult> searchResults,
        CancellationToken cancellationToken = default
    );
}
