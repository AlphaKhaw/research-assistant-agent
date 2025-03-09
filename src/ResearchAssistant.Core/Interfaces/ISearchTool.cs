using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ResearchAssistant.Core.Models;

namespace ResearchAssistant.Core.Interfaces;

public interface ISearchTool
{
    Task<List<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken);
    Task<List<SearchResult>> SearchAsync(
        string query,
        SearchOptions options,
        CancellationToken cancellationToken = default
    );
}
