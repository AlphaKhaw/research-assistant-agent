// using System.Threading;
// using System.Threading.Tasks;
// using Microsoft.Extensions.Logging;
// using ResearchAssistant.Core.Interfaces;
// using ResearchAssistant.Core.Models;

// namespace ResearchAssistant.Core.Services;

// public class PlannerService : IPlanner
// {
//     private readonly IModelConnector _modelConnector;
//     private readonly ISearchTool _searchTool;
//     private readonly ILogger<PlannerService> _logger;

//     public PlannerService(
//         IModelConnector modelConnector,
//         ISearchTool searchTool,
//         ILogger<PlannerService> logger
//     )
//     {
//         _modelConnector = modelConnector;
//         _logger = logger;
//     }

//     // Public Method: Generate Report PLan
//     public async Task<ReportState> GenerateReportPlanASync(
//         ReportState state,
//         CancellationToken cancellationToken = default
//     )
//     {
//         _logger.LogInformation("Generating research plan for topic {Topic}", state.Topic);

//         // Step 1: Generate search queries based on the topic and organisation
//         var searchQueries =
//     }

// }
