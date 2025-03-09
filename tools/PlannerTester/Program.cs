using System;
using System.Collections.Generic;
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

namespace PlannerTester;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("======================================");
        Console.WriteLine("Research Planner Tester");
        Console.WriteLine("======================================");

        // Parse command line arguments
        string topic = "Artificial Intelligence";
        string organization = null;
        string context = "";
        string feedback = "";
        bool useSearch = false;
        float temperature = 0.2f;
        bool reviseMode = false;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--topic" && i + 1 < args.Length)
            {
                topic = args[i + 1];
                i++;
            }
            else if (args[i] == "--organization" && i + 1 < args.Length)
            {
                organization = args[i + 1];
                i++;
            }
            else if (args[i] == "--context" && i + 1 < args.Length)
            {
                context = args[i + 1];
                i++;
            }
            else if (args[i] == "--feedback" && i + 1 < args.Length)
            {
                feedback = args[i + 1];
                reviseMode = true;
                i++;
            }
            else if (args[i] == "--use-search")
            {
                useSearch = true;
            }
            else if (args[i] == "--temperature" && i + 1 < args.Length)
            {
                if (float.TryParse(args[i + 1], out float parsedTemp))
                {
                    temperature = parsedTemp;
                    i++;
                }
            }
            else if (args[i] == "--help" || args[i] == "-h")
            {
                ShowHelp();
                return;
            }
        }

        // Get API credentials from environment variables
        var openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var openAiModel = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o";
        var googleApiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
        var googleSearchEngineId = Environment.GetEnvironmentVariable("GOOGLE_SEARCH_ENGINE_ID");

        // Validate required environment variables
        bool missingEnvVars = false;

        if (string.IsNullOrEmpty(openAiApiKey))
        {
            Console.WriteLine("Error: OPENAI_API_KEY environment variable not set");
            missingEnvVars = true;
        }

        if (useSearch)
        {
            if (string.IsNullOrEmpty(googleApiKey))
            {
                Console.WriteLine(
                    "Error: GOOGLE_API_KEY environment variable not set (required with --use-search)"
                );
                missingEnvVars = true;
            }

            if (string.IsNullOrEmpty(googleSearchEngineId))
            {
                Console.WriteLine(
                    "Error: GOOGLE_SEARCH_ENGINE_ID environment variable not set (required with --use-search)"
                );
                missingEnvVars = true;
            }
        }

        if (missingEnvVars)
        {
            Console.WriteLine("\nPlease set the required environment variables and try again.");
            return;
        }

        try
        {
            // Handle nullable reference types
            if (openAiApiKey == null)
            {
                Console.WriteLine("ERROR: OpenAI API key is missing");
                return;
            }

            if (useSearch && (googleApiKey == null || googleSearchEngineId == null))
            {
                Console.WriteLine("ERROR: Google Search credentials are missing");
                return;
            }

            // Set up dependency injection
            var services = new ServiceCollection();
            services.AddLogging(builder =>
                builder.AddConsole().SetMinimumLevel(LogLevel.Information)
            );

            // Configure JSON serialization
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
            };
            services.AddSingleton(jsonOptions);

            // Create and configure the kernel with OpenAI
            var kernelBuilder = Kernel.CreateBuilder();
            kernelBuilder.AddOpenAIChatCompletion(modelId: openAiModel, apiKey: openAiApiKey);
            var kernel = kernelBuilder.Build();
            services.AddSingleton(kernel);

            // Configure LLM options
            var llmOptions = new LlmOptions
            {
                DefaultTemperature = 0.7f,
                DefaultTopP = 1.0f,
                DefaultMaxTokens = 2000,
            };
            services.AddSingleton(llmOptions);

            // Build service provider
            var serviceProvider = services.BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

            // Create search plugin if requested
            KernelPlugin searchPlugin = null;
            if (useSearch)
            {
                // Create search tool
                var searchLogger = serviceProvider.GetRequiredService<
                    ILogger<GoogleSearchToolWithFunctionCalling>
                >();
                var searchTool = new GoogleSearchToolWithFunctionCalling(
                    googleSearchEngineId,
                    googleApiKey,
                    kernel,
                    jsonOptions,
                    searchLogger
                );

                // Create kernel plugin from search tool
                searchPlugin = KernelPluginFactory.CreateFromObject(searchTool, "SearchPlugin");
                Console.WriteLine("Search capability enabled for planning");
            }
            else
            {
                // Create empty plugin when search is not used
                searchPlugin = KernelPluginFactory.CreateFromFunctions(
                    "SearchPlugin",
                    new List<KernelFunction>()
                );
                Console.WriteLine("Running without search capability");
            }

            // Create OpenAI connector
            var connectorLogger = serviceProvider.GetRequiredService<ILogger<OpenAIConnector>>();
            var connector = new OpenAIConnector(
                kernel,
                searchPlugin,
                llmOptions,
                jsonOptions,
                connectorLogger
            );

            // Create planner service
            var plannerLogger = serviceProvider.GetRequiredService<ILogger<PlannerService>>();
            var planner = new PlannerService(connector, llmOptions, jsonOptions, plannerLogger);

            // Show current settings
            Console.WriteLine("\nPlanner Settings:");
            Console.WriteLine($"- Topic: {topic}");
            Console.WriteLine($"- Organization: {organization}");
            if (!string.IsNullOrEmpty(context))
                Console.WriteLine(
                    $"- Context: {context.Substring(0, Math.Min(50, context.Length))}..."
                );
            Console.WriteLine($"- Model: {openAiModel}");
            Console.WriteLine($"- Temperature: {temperature}");
            Console.WriteLine($"- Search Enabled: {useSearch}");
            if (reviseMode)
                Console.WriteLine($"- Revision Mode: Yes (with feedback)");
            Console.WriteLine("======================================");

            // Create prompt options
            var promptOptions = new PromptOptions
            {
                Temperature = temperature,
                MaxTokens = 2000,
                IncludeCitations = false,
                SearchOptions = useSearch
                    ? new SearchOptions
                    {
                        MaxResults = 3,
                        IncludeUrls = true,
                        IncludeContent = true,
                    }
                    : null,
            };

            if (!reviseMode)
            {
                // Generate initial plan
                Console.WriteLine($"\nGenerating initial plan for topic: {topic}...");
                var plan = await planner.GenerateInitialPlanAsync(
                    topic,
                    organization,
                    context,
                    promptOptions
                );

                // Display the plan
                DisplayPlan(plan);

                // Ask user if they want to revise the plan
                bool wantToRevise = AskUserForRevision();
                if (wantToRevise)
                {
                    feedback = GetUserFeedback();
                    if (!string.IsNullOrWhiteSpace(feedback))
                    {
                        // Revise the plan with feedback
                        Console.WriteLine("\nRevising plan with feedback...");
                        var revisedPlan = await planner.ReviseWithFeedbackAsync(
                            plan,
                            feedback,
                            promptOptions
                        );

                        // Display the revised plan
                        DisplayPlan(revisedPlan);

                        // Ask if user wants to prepare for execution
                        if (AskUserForExecution())
                        {
                            var executionPlan = await planner.PrepareForExecutionAsync(
                                revisedPlan,
                                new ExecutionOptions
                                {
                                    MaxConcurrentSections = 3,
                                    MaxSearchQueriesPerSection = 3,
                                    IncludeReflection = true,
                                }
                            );

                            DisplayExecutionPlan(executionPlan);
                        }
                    }
                }
            }
            else
            {
                // In revision mode, we need an initial plan first
                Console.WriteLine(
                    $"\nGenerating initial plan for topic: {topic} (to be revised)..."
                );
                var initialPlan = await planner.GenerateInitialPlanAsync(
                    topic,
                    organization,
                    context,
                    promptOptions
                );

                // Display the initial plan
                Console.WriteLine("\nINITIAL PLAN:");
                DisplayPlan(initialPlan);

                // Revise with the provided feedback
                Console.WriteLine($"\nRevising plan with feedback: {feedback}");
                var revisedPlan = await planner.ReviseWithFeedbackAsync(
                    initialPlan,
                    feedback,
                    promptOptions
                );

                // Display the revised plan
                Console.WriteLine("\nREVISED PLAN:");
                DisplayPlan(revisedPlan);
            }

            Console.WriteLine("\nThank you for using the Research Planner Tester!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nAn unexpected error occurred: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
            }
            Console.WriteLine(ex.StackTrace);
        }
    }

    private static void DisplayPlan(ReportPlan plan)
    {
        Console.WriteLine("\n======================================");
        Console.WriteLine($"PLAN: {plan.Topic}");
        Console.WriteLine($"Created: {plan.CreatedAt}");
        Console.WriteLine($"Tokens Used: {plan.TokensUsed}");
        Console.WriteLine("======================================");

        if (plan.Sections == null || plan.Sections.Count == 0)
        {
            Console.WriteLine("No sections found in the plan.");
            return;
        }

        foreach (var section in plan.Sections)
        {
            Console.WriteLine($"\n{section.Number}. {section.Name}");
            Console.WriteLine($"   Description: {section.Description}");
            Console.WriteLine($"   Requires Research: {section.RequiresResearch}");
        }

        if (plan.RevisionHistory != null && plan.RevisionHistory.Count > 0)
        {
            Console.WriteLine("\n--- Revision History ---");
            foreach (var revision in plan.RevisionHistory)
            {
                Console.WriteLine($"Revision at {revision.Timestamp}");
                Console.WriteLine($"Feedback: {revision.Feedback}");
            }
        }
    }

    private static void DisplayExecutionPlan(ExecutionPlan plan)
    {
        Console.WriteLine("\n======================================");
        Console.WriteLine($"EXECUTION PLAN: {plan.Topic}");
        Console.WriteLine($"Plan ID: {plan.PlanId}");
        Console.WriteLine($"Status: {plan.Status}");
        Console.WriteLine($"Created: {plan.CreatedAt}");
        Console.WriteLine($"Max Concurrent Sections: {plan.MaxConcurrentSections}");
        Console.WriteLine("======================================");

        if (plan.ResearchTasks == null || plan.ResearchTasks.Count == 0)
        {
            Console.WriteLine("No research tasks found in the execution plan.");
            return;
        }

        Console.WriteLine("\n--- Research Tasks ---");
        foreach (var task in plan.ResearchTasks)
        {
            Console.WriteLine($"\nSection: {task.SectionName}");
            Console.WriteLine($"Status: {task.Status}");
            Console.WriteLine($"Max Search Queries: {task.MaxSearchQueries}");
        }
    }

    private static bool AskUserForRevision()
    {
        Console.Write("\nDo you want to revise this plan? (y/n): ");
        var response = Console.ReadLine()?.Trim().ToLower();
        return response == "y" || response == "yes";
    }

    private static bool AskUserForExecution()
    {
        Console.Write("\nDo you want to prepare this plan for execution? (y/n): ");
        var response = Console.ReadLine()?.Trim().ToLower();
        return response == "y" || response == "yes";
    }

    private static string GetUserFeedback()
    {
        Console.WriteLine("\nEnter your feedback for the plan (press Enter twice when done):");
        var lines = new List<string>();
        string line;

        while (!string.IsNullOrWhiteSpace(line = Console.ReadLine()))
        {
            lines.Add(line);
        }

        return string.Join("\n", lines);
    }

    private static void ShowHelp()
    {
        Console.WriteLine("Research Planner Tester - Help");
        Console.WriteLine("======================================");
        Console.WriteLine("Available command line arguments:");
        Console.WriteLine(
            "  --topic <text>          Set the report topic (default: \"Artificial Intelligence\")"
        );
        Console.WriteLine("  --organization <text>   Set the report organization structure");
        Console.WriteLine("  --context <text>        Provide context for planning");
        Console.WriteLine("  --feedback <text>       Provide feedback for plan revision");
        Console.WriteLine("  --use-search            Enable search capability for planning");
        Console.WriteLine("  --temperature <number>  Set temperature (0.0-1.0, default: 0.2)");
        Console.WriteLine("  --help, -h              Show this help message");
        Console.WriteLine("\nExamples:");
        Console.WriteLine(
            "  Generate plan: make test-planner ARGS=\"--topic 'Quantum Computing'\""
        );
        Console.WriteLine(
            "  Revise plan:   make test-planner ARGS=\"--topic 'Climate Change' --feedback 'Add more about mitigation strategies'\""
        );
    }
}
