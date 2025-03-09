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
using ResearchAssistant.Core.Interfaces;
using ResearchAssistant.Core.Models;
using ResearchAssistant.Core.Services;
using ResearchAssistant.Core.Services.LLM;
using ResearchAssistant.Core.Services.Search;
using TaskState = ResearchAssistant.Core.Models.TaskStatus;

namespace ResearchAssistant.Cli;

class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        ConsoleColor defaultColor = Console.ForegroundColor;

        PrintHeader("RESEARCH ASSISTANT", ConsoleColor.Cyan);

        // Parse command line arguments
        var options = ParseCommandLineArgs(args);
        if (options.ShowHelp)
        {
            ShowHelp();
            return 0;
        }

        if (!ValidateEnvironmentVariables(options))
        {
            return 1;
        }

        try
        {
            // Configure services
            ServiceProvider serviceProvider;
            try
            {
                var services = ConfigureServices(options);
                serviceProvider = services.BuildServiceProvider();

                PrintSuccess("Services initialized successfully");
            }
            catch (Exception ex)
            {
                PrintError($"Failed to initialize services: {ex.Message}");
                return 1;
            }

            // Get the research topic if not provided in arguments
            if (string.IsNullOrWhiteSpace(options.Topic))
            {
                PrintPrompt("Enter research topic: ");
                options.Topic = Console.ReadLine() ?? "";
                if (string.IsNullOrWhiteSpace(options.Topic))
                {
                    PrintError("Research topic is required.");
                    return 1;
                }
            }

            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Starting research assistant for topic: {Topic}", options.Topic);

            // Get services
            var planner = serviceProvider.GetRequiredService<IPlanner>();
            var sectionWriter = serviceProvider.GetRequiredService<ISectionWriter>();

            // Generate initial plan
            PrintStage("PLANNING STAGE");
            PrintInfo($"Generating initial plan for topic: {options.Topic}");

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
            PrintHeader("INITIAL RESEARCH PLAN", ConsoleColor.Yellow);
            DisplayPlan(plan);

            // Ask for feedback & revision until satisfied
            bool needsRevision = true;
            int revisionCount = 0;

            while (needsRevision && revisionCount < 3) // Limit to 3 revisions
            {
                // Ask if user wants to revise the plan
                needsRevision = AskUserForRevision();
                if (needsRevision)
                {
                    revisionCount++;
                    PrintPrompt(
                        "Enter your feedback for the plan (press Enter twice when done):\n"
                    );
                    string feedback = GetUserFeedback();

                    if (string.IsNullOrWhiteSpace(feedback))
                    {
                        PrintWarning("No feedback provided, skipping revision");
                        needsRevision = false;
                        continue;
                    }

                    PrintInfo($"Revising plan with feedback (revision #{revisionCount})...");
                    logger.LogInformation("Revising plan with feedback: {Feedback}", feedback);

                    plan = await planner.ReviseWithFeedbackAsync(plan, feedback, promptOptions);

                    PrintHeader($"REVISED RESEARCH PLAN (#{revisionCount})", ConsoleColor.Yellow);
                    DisplayPlan(plan);

                    logger.LogInformation("Plan revised successfully");
                }
            }

            if (needsRevision && revisionCount >= 3)
            {
                PrintWarning("Maximum number of revisions reached, proceeding with current plan");
            }

            // Prepare plan for execution
            PrintStage("EXECUTION PREPARATION");
            PrintInfo("Preparing plan for execution...");

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

            PrintHeader("EXECUTION PLAN", ConsoleColor.Yellow);
            DisplayExecutionPlan(executionPlan);

            if (!AskUserToProceed("Do you want to proceed with report generation?"))
            {
                PrintInfo("Report generation cancelled by user");
                return 0;
            }

            // Execute the plan to generate the report
            PrintStage("REPORT GENERATION");
            PrintInfo("Generating research report (this may take several minutes)...");

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
            PrintStage("COMPLETED REPORT");
            DisplayReport(report);

            // Save report to file
            string reportFilePath = await SaveReportToFile(report, options.Topic);
            PrintSuccess($"Report saved to: {reportFilePath}");

            PrintStage("COMPLETION SUMMARY");
            PrintStats(report, executionPlan);

            return 0;
        }
        catch (Exception ex)
        {
            PrintError($"An error occurred: {ex.Message}");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("Stack trace:");
            Console.WriteLine(ex.StackTrace);
            Console.ForegroundColor = defaultColor;
            return 1;
        }
    }

    #region Command Line Parsing

    private class CommandLineOptions
    {
        public string Topic { get; set; } = "";
        public string Organization { get; set; } = "";
        public string Context { get; set; } = "";
        public bool UseSearch { get; set; } = true;
        public float Temperature { get; set; } = 0.2f;
        public int MaxConcurrentSections { get; set; } = 2;
        public int MaxSearchQueries { get; set; } = 3;
        public bool ShowHelp { get; set; } = false;
        public string OutputDirectory { get; set; } = "";
    }

    private static CommandLineOptions ParseCommandLineArgs(string[] args)
    {
        var options = new CommandLineOptions();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "--topic" when i + 1 < args.Length:
                    options.Topic = args[++i];
                    break;

                case "--organization" when i + 1 < args.Length:
                    options.Organization = args[++i];
                    break;

                case "--context" when i + 1 < args.Length:
                    options.Context = args[++i];
                    break;

                case "--no-search":
                    options.UseSearch = false;
                    break;

                case "--temperature" when i + 1 < args.Length:
                    if (float.TryParse(args[++i], out float temp))
                        options.Temperature = temp;
                    break;

                case "--max-concurrent" when i + 1 < args.Length:
                    if (int.TryParse(args[++i], out int concurrent))
                        options.MaxConcurrentSections = concurrent;
                    break;

                case "--max-queries" when i + 1 < args.Length:
                    if (int.TryParse(args[++i], out int queries))
                        options.MaxSearchQueries = queries;
                    break;

                case "--output" when i + 1 < args.Length:
                    options.OutputDirectory = args[++i];
                    break;

                case "--help":
                case "-h":
                    options.ShowHelp = true;
                    break;
            }
        }

        // Use the current directory if not specified
        if (string.IsNullOrEmpty(options.OutputDirectory))
        {
            options.OutputDirectory = Environment.CurrentDirectory;
        }

        return options;
    }

    private static void ShowHelp()
    {
        Console.WriteLine("Research Assistant CLI - Help");
        Console.WriteLine("======================================");
        Console.WriteLine("Available command line arguments:");
        Console.WriteLine("  --topic <text>          Research topic to investigate");
        Console.WriteLine(
            "  --organization <text>   Specific organization structure for the report"
        );
        Console.WriteLine("  --context <text>        Additional context for the research");
        Console.WriteLine("  --no-search             Disable web search capability");
        Console.WriteLine("  --temperature <number>  Set temperature (0.0-1.0, default: 0.2)");
        Console.WriteLine("  --max-concurrent <num>  Maximum concurrent sections (default: 2)");
        Console.WriteLine(
            "  --max-queries <num>     Maximum search queries per section (default: 3)"
        );
        Console.WriteLine(
            "  --output <directory>    Directory to save the report (default: current)"
        );
        Console.WriteLine("  --help, -h              Show this help message");
        Console.WriteLine("\nExamples:");
        Console.WriteLine("  researcha --topic \"Quantum Computing\" --max-concurrent 3");
        Console.WriteLine("  researcha --topic \"Climate Change\" --no-search --temperature 0.1");
        Console.WriteLine("\nRequired Environment Variables:");
        Console.WriteLine("  OPENAI_API_KEY          Your OpenAI API key");
        Console.WriteLine("  OPENAI_MODEL            Model to use (default: gpt-4o)");
        Console.WriteLine("  GOOGLE_API_KEY          Your Google API key (for search)");
        Console.WriteLine("  GOOGLE_SEARCH_ENGINE_ID Your Google Search Engine ID (for search)");
    }

    private static bool ValidateEnvironmentVariables(CommandLineOptions options)
    {
        bool isValid = true;

        var openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrEmpty(openAiApiKey))
        {
            PrintError("OPENAI_API_KEY environment variable not set");
            isValid = false;
        }

        if (options.UseSearch)
        {
            var googleApiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
            if (string.IsNullOrEmpty(googleApiKey))
            {
                PrintError("GOOGLE_API_KEY environment variable not set (required for search)");
                isValid = false;
            }

            var googleSearchEngineId = Environment.GetEnvironmentVariable(
                "GOOGLE_SEARCH_ENGINE_ID"
            );
            if (string.IsNullOrEmpty(googleSearchEngineId))
            {
                PrintError(
                    "GOOGLE_SEARCH_ENGINE_ID environment variable not set (required for search)"
                );
                isValid = false;
            }
        }

        if (!isValid)
        {
            PrintInfo("\nPlease set the required environment variables and try again.");
        }

        return isValid;
    }

    #endregion

    #region Service Configuration

    private static ServiceCollection ConfigureServices(CommandLineOptions options)
    {
        var services = new ServiceCollection();

        // Add logging
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

        // Get API keys from environment
        var openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";
        var openAiModel = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o";
        var googleApiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
        var googleSearchEngineId = Environment.GetEnvironmentVariable("GOOGLE_SEARCH_ENGINE_ID");

        // Add Kernel
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.AddOpenAIChatCompletion(modelId: openAiModel, apiKey: openAiApiKey);
        var kernel = kernelBuilder.Build();
        services.AddSingleton(kernel);

        // Add JSON options
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };
        services.AddSingleton(jsonOptions);

        // Add LLM options
        var llmOptions = new LlmOptions
        {
            ApiKey = openAiApiKey,
            ModelId = openAiModel,
            DefaultTemperature = options.Temperature,
            DefaultMaxTokens = 4096,
            EnableFunctionCalling = true,
        };
        services.AddSingleton(llmOptions);

        // Add search plugin and tool
        if (
            options.UseSearch
            && !string.IsNullOrEmpty(googleApiKey)
            && !string.IsNullOrEmpty(googleSearchEngineId)
        )
        {
            // Register the search tool with a factory to properly resolve dependencies
            services.AddSingleton<ISearchTool>(sp =>
            {
                // The service provider will resolve the logger dependency
                var searchLogger = sp.GetRequiredService<
                    ILogger<GoogleSearchToolWithFunctionCalling>
                >();

                var searchTool = new GoogleSearchToolWithFunctionCalling(
                    googleSearchEngineId,
                    googleApiKey,
                    sp.GetRequiredService<Kernel>(),
                    sp.GetRequiredService<JsonSerializerOptions>(),
                    searchLogger // Correctly passing the logger
                );

                return searchTool;
            });

            // Get the search plugin (needs to happen after service provider is built)
            services.AddSingleton(sp =>
            {
                return
                    sp.GetRequiredService<ISearchTool>()
                        is GoogleSearchToolWithFunctionCalling googleSearch
                    ? googleSearch.GetSearchPlugin()
                    : KernelPluginFactory.CreateFromFunctions(
                        "SearchPlugin",
                        new List<KernelFunction>()
                    );
            });
        }
        else
        {
            // Create dummy search plugin when search is not used
            services.AddSingleton<ISearchTool, DummySearchTool>();

            // Register empty search plugin
            services.AddSingleton<KernelPlugin>(sp =>
                KernelPluginFactory.CreateFromFunctions("SearchPlugin", new List<KernelFunction>())
            );
        }

        // Add OpenAI connector
        services.AddSingleton<IModelConnector, OpenAIConnector>();

        // Add planner
        services.AddSingleton<IPlanner, PlannerService>();

        // Add section writer
        services.AddSingleton<ISectionWriter, SectionWriter>();

        return services;
    }

    #endregion

    #region Display Methods

    private static void DisplayPlan(ReportPlan plan)
    {
        if (plan == null)
        {
            PrintWarning("No plan available to display");
            return;
        }

        PrintInfo($"Report Topic: {plan.Topic}");
        PrintInfo($"Created: {plan.CreatedAt.ToLocalTime():g}");
        PrintInfo($"Tokens Used: {plan.TokensUsed}");
        Console.WriteLine();

        if (plan.Sections == null || plan.Sections.Count == 0)
        {
            PrintWarning("No sections found in the plan.");
            return;
        }

        PrintHeader("SECTIONS", ConsoleColor.Yellow);

        foreach (var section in plan.Sections)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"{section.Number}. {section.Name}");
            Console.ResetColor();

            Console.WriteLine($"   Description: {section.Description}");

            if (section.RequiresResearch)
            {
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine($"   Requires Research: Yes");
                Console.ResetColor();
            }

            Console.WriteLine();
        }

        if (plan.RevisionHistory != null && plan.RevisionHistory.Count > 0)
        {
            PrintHeader("REVISION HISTORY", ConsoleColor.DarkYellow);

            foreach (var revision in plan.RevisionHistory)
            {
                Console.WriteLine($"Revision at {revision.Timestamp.ToLocalTime():g}");
                Console.WriteLine($"Feedback: {revision.Feedback}");
                Console.WriteLine();
            }
        }
    }

    private static void DisplayExecutionPlan(ExecutionPlan plan)
    {
        if (plan == null)
        {
            PrintWarning("No execution plan available to display");
            return;
        }

        PrintInfo($"Topic: {plan.Topic}");
        PrintInfo($"Plan ID: {plan.PlanId}");
        PrintInfo($"Status: {plan.Status}");
        PrintInfo($"Created: {plan.CreatedAt.ToLocalTime():g}");
        PrintInfo($"Max Concurrent Sections: {plan.MaxConcurrentSections}");
        Console.WriteLine();

        if (plan.ResearchTasks == null || plan.ResearchTasks.Count == 0)
        {
            PrintWarning("No research tasks found in the execution plan.");
            return;
        }

        PrintHeader("RESEARCH TASKS", ConsoleColor.Yellow);

        foreach (var task in plan.ResearchTasks)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Section: {task.SectionName}");
            Console.ResetColor();

            Console.WriteLine($"Status: {task.Status}");
            Console.WriteLine($"Max Search Queries: {task.MaxSearchQueries}");

            if (task.Phase != null)
            {
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine($"Phase: {task.Phase}");
                Console.ResetColor();
            }

            Console.WriteLine();
        }
    }

    private static void DisplayReport(Report report)
    {
        if (report == null)
        {
            PrintWarning("No report available to display");
            return;
        }

        PrintInfo($"Report Topic: {report.Topic}");
        PrintInfo($"Created: {report.CreatedAt.ToLocalTime():g}");
        PrintInfo($"Sections: {report.Sections?.Count ?? 0}");
        PrintInfo($"Citations: {report.Citations?.Count ?? 0}");
        Console.WriteLine();

        if (report.Sections == null || report.Sections.Count == 0)
        {
            PrintWarning("No sections found in the report.");
            return;
        }

        // Sort sections by section number
        var orderedSections = report.Sections.OrderBy(s => s.SectionNumber).ToList();

        PrintHeader("REPORT SECTIONS", ConsoleColor.Magenta);

        foreach (var section in orderedSections)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"\n{section.SectionNumber}. {section.SectionName}");
            Console.ResetColor();

            if (section.IsRevised)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("(Revised)");
                Console.ResetColor();
            }

            // Only show a preview of the content
            var contentLines = section.Content.Split('\n');
            var previewLines = contentLines.Take(Math.Min(4, contentLines.Length)).ToList();

            foreach (var line in previewLines)
            {
                Console.WriteLine(line);
            }

            if (contentLines.Length > 4)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("... (content truncated for preview) ...");
                Console.ResetColor();
            }

            Console.WriteLine();
        }

        if (report.Citations != null && report.Citations.Any())
        {
            PrintHeader("CITATIONS", ConsoleColor.DarkCyan);

            foreach (var citation in report.Citations.OrderBy(c => c.Number))
            {
                Console.WriteLine($"[{citation.Number}] {citation.Title}");
                Console.WriteLine($"    URL: {citation.Url}");
                Console.WriteLine();
            }
        }
    }

    private static void PrintStats(Report report, ExecutionPlan plan)
    {
        Console.WriteLine(new string('=', 50));
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("RESEARCH REPORT STATISTICS");
        Console.ResetColor();
        Console.WriteLine(new string('=', 50));

        // Basic stats
        Console.WriteLine($"Topic: {report.Topic}");
        Console.WriteLine($"Total sections: {report.Sections?.Count ?? 0}");
        Console.WriteLine($"Total citations: {report.Citations?.Count ?? 0}");

        // Token usage
        Console.WriteLine($"Total tokens used: {report.TokensUsed:N0}");

        // Timing
        var startTime = plan.StartedAt ?? plan.CreatedAt;
        var endTime = plan.CompletedAt ?? DateTime.UtcNow;
        var duration = endTime - startTime;

        Console.WriteLine($"Generation time: {duration.TotalMinutes:N1} minutes");

        // Section information
        if (report.Sections != null && report.Sections.Count > 0)
        {
            int totalWords = report.Sections.Sum(s => CountWords(s.Content));

            Console.WriteLine($"Total word count: {totalWords:N0}");
            Console.WriteLine(
                $"Average words per section: {totalWords / report.Sections.Count:N0}"
            );
        }

        Console.WriteLine(new string('=', 50));
    }

    private static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        return text.Split(
            new[] { ' ', '\t', '\n', '\r' },
            StringSplitOptions.RemoveEmptyEntries
        ).Length;
    }

    #endregion

    #region User Interaction Methods

    private static bool AskUserForRevision()
    {
        PrintPrompt("Do you want to revise this plan? (y/n): ");
        var response = Console.ReadLine()?.Trim().ToLower();
        return response == "y" || response == "yes";
    }

    private static bool AskUserToProceed(string question)
    {
        PrintPrompt($"{question} (y/n): ");
        var response = Console.ReadLine()?.Trim().ToLower();
        return response == "y" || response == "yes";
    }

    private static string GetUserFeedback()
    {
        var lines = new List<string>();
        string line;

        while (!string.IsNullOrWhiteSpace(line = Console.ReadLine()))
        {
            lines.Add(line);
        }

        return string.Join("\n", lines);
    }

    #endregion

    #region Console Utilities

    private static void PrintHeader(string text, ConsoleColor color)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 50));
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ResetColor();
        Console.WriteLine(new string('=', 50));
    }

    private static void PrintStage(string stageName)
    {
        Console.WriteLine();
        Console.WriteLine(new string('-', 50));
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($">>> {stageName} <<<");
        Console.ResetColor();
        Console.WriteLine(new string('-', 50));
    }

    private static void PrintInfo(string message)
    {
        Console.WriteLine(message);
    }

    private static void PrintSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✓ {message}");
        Console.ResetColor();
    }

    private static void PrintWarning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"⚠ {message}");
        Console.ResetColor();
    }

    private static void PrintError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"❌ {message}");
        Console.ResetColor();
    }

    private static void PrintPrompt(string prompt)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write(prompt);
        Console.ResetColor();
    }

    #endregion

    #region File Operations

    private static async Task<string> SaveReportToFile(Report report, string topic)
    {
        // Create a valid filename from the topic
        string sanitizedTopic = string.Join("_", topic.Split(Path.GetInvalidFileNameChars()));
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string filename = $"research_report_{sanitizedTopic}_{timestamp}.md";
        string filePath = Path.Combine(Environment.CurrentDirectory, filename);

        using var writer = new StreamWriter(filePath);

        // Write header
        await writer.WriteLineAsync($"# {report.Topic}");
        await writer.WriteLineAsync($"\nGenerated on {report.CreatedAt.ToLocalTime():f}\n");

        // Write table of contents
        await writer.WriteLineAsync("## Table of Contents");
        foreach (var section in report.Sections.OrderBy(s => s.SectionNumber))
        {
            await writer.WriteLineAsync(
                $"{section.SectionNumber}. [{section.SectionName}](#{section.SectionName.Replace(" ", "-").ToLower()})"
            );
        }
        await writer.WriteLineAsync();

        // Write each section
        foreach (var section in report.Sections.OrderBy(s => s.SectionNumber))
        {
            await writer.WriteLineAsync($"## {section.SectionNumber}. {section.SectionName}");
            await writer.WriteLineAsync(section.Content);
            await writer.WriteLineAsync();
        }

        // Write citations if available
        if (report.Citations != null && report.Citations.Any())
        {
            await writer.WriteLineAsync("## References");

            foreach (var citation in report.Citations.OrderBy(c => c.Number))
            {
                await writer.WriteLineAsync(
                    $"[{citation.Number}] {citation.Title}. {citation.Url}"
                );
            }
        }

        return filePath;
    }

    #endregion
}

/// <summary>
/// Reports progress of section writing
/// </summary>
internal class ProgressReporter : IDisposable
{
    private readonly ExecutionPlan _plan;
    private readonly Dictionary<string, TaskState> _lastReportedStatus = new();
    private readonly object _lock = new();

    public ProgressReporter(ExecutionPlan plan)
    {
        _plan = plan;

        // Initialize with current status
        foreach (var task in _plan.ResearchTasks)
        {
            _lastReportedStatus[task.SectionId] = task.Status;
        }
    }

    public async Task ReportProgressAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                ReportProgress();
                await Task.Delay(2000, cancellationToken); // Update every 2 seconds
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in progress reporting: {ex.Message}");
            }
        }
    }

    private void ReportProgress()
    {
        lock (_lock)
        {
            int completed = 0;
            int inProgress = 0;
            int pending = 0;
            int failed = 0;

            // Count statuses
            foreach (var task in _plan.ResearchTasks)
            {
                switch (task.Status)
                {
                    case TaskState.Completed:
                        completed++;
                        break;
                    case TaskState.InProgress:
                        inProgress++;
                        break;
                    case TaskState.Pending:
                        pending++;
                        break;
                    case TaskState.Failed:
                        failed++;
                        break;
                }

                // Report changes since last check
                if (_lastReportedStatus[task.SectionId] != task.Status)
                {
                    ReportStatusChange(
                        task.SectionName,
                        _lastReportedStatus[task.SectionId],
                        task.Status
                    );
                    _lastReportedStatus[task.SectionId] = task.Status;
                }
            }

            // Report overall progress
            int total = _plan.ResearchTasks.Count;
            double percentComplete = (double)completed / total * 100;

            // Clear current line and write progress
            Console.Write(
                $"\rProgress: {percentComplete:F1}% | Completed: {completed}/{total} | In Progress: {inProgress} | Pending: {pending} | Failed: {failed}"
            );
        }
    }

    private void ReportStatusChange(string sectionName, TaskState oldStatus, TaskState newStatus)
    {
        // Only report meaningful changes
        if (oldStatus == newStatus)
            return;

        Console.WriteLine();
        Console.ForegroundColor = newStatus switch
        {
            TaskState.Completed => ConsoleColor.Green,
            TaskState.InProgress => ConsoleColor.Cyan,
            TaskState.Failed => ConsoleColor.Red,
            _ => Console.ForegroundColor,
        };

        Console.WriteLine($"Section \"{sectionName}\" is now {newStatus} (was {oldStatus})");
        Console.ResetColor();
    }

    public void Dispose()
    {
        // Clean up if needed
    }
}

/// <summary>
/// A dummy search tool for when search is disabled
/// </summary>
internal class DummySearchTool : ISearchTool
{
    private readonly ILogger<DummySearchTool> _logger;

    public DummySearchTool(ILogger<DummySearchTool> logger)
    {
        _logger = logger;
    }

    public Task<List<SearchResult>> SearchAsync(
        string query,
        SearchOptions options = null,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("Dummy search tool called with query: {Query}", query);

        return Task.FromResult(new List<SearchResult>());
    }

    public Task<List<SearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default
    )
    {
        return SearchAsync(query, null, cancellationToken);
    }
}
