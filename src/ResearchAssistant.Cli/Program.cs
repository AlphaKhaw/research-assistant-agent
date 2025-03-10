using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ResearchAssistant.Cli.Services;
using ResearchAssistant.Cli.UI;
using ResearchAssistant.Cli.UI.ProgressReporting;
using ResearchAssistant.Cli.Utils;
using ResearchAssistant.Core.Interfaces;
using ResearchAssistant.Core.Models;
using ResearchAssistant.Core.Services;
using ResearchAssistant.Core.Services.LLM;
using ResearchAssistant.Core.Services.Search;
using TaskState = ResearchAssistant.Core.Models.TaskStatus;

namespace ResearchAssistant.Cli;

/// <summary>
/// Main entry point for the Research Assistant command-line application.
/// Provides functionality for generating research reports based on user-specified topics.
/// </summary>
class Program
{
    /// <summary>
    /// Entry point for the Research Assistant application.
    /// Handles argument parsing, service initialization, plan generation, and report creation.
    /// </summary>
    /// <param name="args">Command-line arguments passed to the application</param>
    /// <returns>
    /// 0 for successful execution, 1 for errors
    /// </returns>
    static async Task<int> Main(string[] args)
    {
        // Set console output configuration
        Console.OutputEncoding = Encoding.UTF8;
        ConsoleColor defaultColor = Console.ForegroundColor;

        ConsoleUI.PrintHeader("RESEARCH ASSISTANT", ConsoleColor.Cyan);

        // Parse command line arguments
        var options = CommandLineParser.ParseCommandLineArgs(args);
        if (options.ShowHelp)
        {
            CommandLineParser.ShowHelp();
            return 0;
        }

        if (!CommandLineParser.ValidateEnvironmentVariables(options))
        {
            return 1;
        }

        try
        {
            // Configure services
            ServiceProvider serviceProvider;
            try
            {
                var services = ConfigurationService.ConfigureServices(options);
                serviceProvider = services.BuildServiceProvider();

                ConsoleUI.PrintSuccess("Services initialized successfully");
            }
            catch (Exception ex)
            {
                ConsoleUI.PrintError($"Failed to initialize services: {ex.Message}");
                return 1;
            }

            // Get the research topic if not provided in arguments
            if (string.IsNullOrWhiteSpace(options.Topic))
            {
                ConsoleUI.PrintPrompt("Enter research topic: ");
                options.Topic = Console.ReadLine() ?? "";
                if (string.IsNullOrWhiteSpace(options.Topic))
                {
                    ConsoleUI.PrintError("Research topic is required.");
                    return 1;
                }
            }

            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Starting research assistant for topic: {Topic}", options.Topic);

            // Get services
            var planner = serviceProvider.GetRequiredService<IPlanner>();
            var sectionWriter = serviceProvider.GetRequiredService<ISectionWriter>();

            // Generate initial plan
            ConsoleUI.PrintStage("PLANNING STAGE");
            ConsoleUI.PrintInfo($"Generating initial plan for topic: {options.Topic}");

            var promptOptions = new PromptOptions
            {
                Temperature = options.Temperature,
                MaxTokens = 2000,
                SearchOptions = options.UseSearch
                    ? new SearchOptions
                    {
                        MaxResults = 3,
                        IncludeUrls = true,
                        IncludeContent = true,
                    }
                    : null,
            };

            var plan = await planner.GenerateInitialPlanAsync(
                options.Topic,
                options.Organization,
                options.Context,
                promptOptions
            );

            logger.LogInformation(
                "Initial plan generated with {SectionCount} sections",
                plan.Sections?.Count ?? 0
            );

            // Display the plan
            ConsoleUI.PrintHeader("INITIAL RESEARCH PLAN", ConsoleColor.Yellow);
            ConsoleUI.DisplayPlan(plan);

            // Ask for feedback & revision until satisfied
            bool needsRevision = true;
            int revisionCount = 0;

            while (needsRevision && revisionCount < 3) // Limit to 3 revisions
            {
                // Ask if user wants to revise the plan
                needsRevision = UserInteraction.AskUserForRevision();
                if (needsRevision)
                {
                    revisionCount++;
                    ConsoleUI.PrintPrompt(
                        "Enter your feedback for the plan (press Enter twice when done):\n"
                    );
                    string feedback = UserInteraction.GetUserFeedback();

                    if (string.IsNullOrWhiteSpace(feedback))
                    {
                        ConsoleUI.PrintWarning("No feedback provided, skipping revision");
                        needsRevision = false;
                        continue;
                    }

                    ConsoleUI.PrintInfo(
                        $"Revising plan with feedback (revision #{revisionCount})..."
                    );
                    logger.LogInformation("Revising plan with feedback: {Feedback}", feedback);

                    plan = await planner.ReviseWithFeedbackAsync(plan, feedback, promptOptions);

                    ConsoleUI.PrintHeader(
                        $"REVISED RESEARCH PLAN (#{revisionCount})",
                        ConsoleColor.Yellow
                    );
                    ConsoleUI.DisplayPlan(plan);

                    logger.LogInformation("Plan revised successfully");
                }
            }

            if (needsRevision && revisionCount >= 3)
            {
                ConsoleUI.PrintWarning(
                    "Maximum number of revisions reached, proceeding with current plan"
                );
            }

            // Prepare plan for execution
            ConsoleUI.PrintStage("EXECUTION PREPARATION");
            ConsoleUI.PrintInfo("Preparing plan for execution...");

            var executionOptions = new ExecutionOptions
            {
                MaxConcurrentSections = options.MaxConcurrentSections,
                MaxSearchQueriesPerSection = options.MaxSearchQueries,
                IncludeReflection = true,
            };

            var executionPlan = await planner.PrepareForExecutionAsync(plan, executionOptions);
            logger.LogInformation(
                "Execution plan prepared with {TaskCount} research tasks",
                executionPlan.ResearchTasks?.Count ?? 0
            );

            ConsoleUI.PrintHeader("EXECUTION PLAN", ConsoleColor.Yellow);
            ConsoleUI.DisplayExecutionPlan(executionPlan);

            if (!UserInteraction.AskUserToProceed("Do you want to proceed with report generation?"))
            {
                ConsoleUI.PrintInfo("Report generation cancelled by user");
                return 0;
            }

            // Execute the plan to generate the report
            ConsoleUI.PrintStage("REPORT GENERATION");
            ConsoleUI.PrintInfo("Generating research report (this may take several minutes)...");

            using var progressReporter = new ProgressReporter(executionPlan);
            var cts = new CancellationTokenSource();

            // Start progress reporting task
            var progressTask = Task.Run(() => progressReporter.ReportProgressAsync(cts.Token));

            // Execute the plan
            var report = await sectionWriter.WriteReportAsync(executionPlan, cts.Token);

            // Stop progress reporting
            cts.Cancel();
            try
            {
                await progressTask;
            }
            catch (OperationCanceledException) { }

            logger.LogInformation(
                "Report generated successfully with {SectionCount} sections",
                report.Sections?.Count ?? 0
            );

            // Display and save the final report
            ConsoleUI.PrintStage("COMPLETED REPORT");
            ConsoleUI.DisplayReport(report);

            // Save report to file
            string reportFilePath = await FileOperations.SaveReportToFile(report, options.Topic);
            ConsoleUI.PrintSuccess($"Report saved to: {reportFilePath}");

            ConsoleUI.PrintStage("COMPLETION SUMMARY");
            ConsoleUI.PrintStats(report, executionPlan);

            return 0;
        }
        catch (Exception ex)
        {
            ConsoleUI.PrintError($"An error occurred: {ex.Message}");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("Stack trace:");
            Console.WriteLine(ex.StackTrace);
            Console.ForegroundColor = defaultColor;
            return 1;
        }
    }
}

/// <summary>
/// A dummy implementation of ISearchTool that returns empty results.
/// Used when search functionality is disabled.
/// </summary>
internal class DummySearchTool : ISearchTool
{
    private readonly ILogger<DummySearchTool> _logger;

    /// <summary>
    /// Initializes a new instance of the DummySearchTool class.
    /// </summary>
    /// <param name="logger">Logger for recording search queries</param>
    public DummySearchTool(ILogger<DummySearchTool> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Simulates a search operation by logging the query and returning an empty result list.
    /// </summary>
    /// <param name="query">The search query</param>
    /// <param name="options">Optional search configuration options</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests</param>
    /// <returns>An empty list of search results</returns>
    public Task<List<SearchResult>> SearchAsync(
        string query,
        SearchOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("Dummy search tool called with query: {Query}", query);

        return Task.FromResult(new List<SearchResult>());
    }

    /// <summary>
    /// Simulates a search operation by logging the query and returning an empty result list.
    /// Simplified overload that calls the main method with null options.
    /// </summary>
    /// <param name="query">The search query</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests</param>
    /// <returns>An empty list of search results</returns>
    public Task<List<SearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default
    )
    {
        return SearchAsync(query, null, cancellationToken);
    }
}
