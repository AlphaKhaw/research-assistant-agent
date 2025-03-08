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
using ResearchAssistant.Core.Services.LLM;
using ResearchAssistant.Core.Services.Search;

namespace ConnectorTester;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("======================================");
        Console.WriteLine("OpenAI Connector Tester");
        Console.WriteLine("======================================");

        // Parse command line arguments
        bool useSearch = false;
        float? temperature = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--use-search")
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
                DefaultMaxTokens = 1000,
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

                // Register search function with the kernel
                searchPlugin = KernelPluginFactory.CreateFromObject(searchTool, "SearchPlugin");
                Console.WriteLine("Search capability enabled");
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

            // Show current settings
            Console.WriteLine("\nConnector Settings:");
            Console.WriteLine($"- Model: {openAiModel}");
            Console.WriteLine($"- Temperature: {(temperature ?? llmOptions.DefaultTemperature)}");
            Console.WriteLine($"- Search Enabled: {useSearch}");
            Console.WriteLine("======================================");

            // Interactive prompt loop
            bool continuePrompting = true;

            while (continuePrompting)
            {
                Console.WriteLine("\n======================================");
                Console.Write("Enter prompt (or 'exit' to quit): ");
                var prompt = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(prompt) || prompt.Trim().ToLower() == "exit")
                {
                    continuePrompting = false;
                    continue;
                }

                await ProcessPrompt(connector, prompt, temperature, useSearch);
            }

            Console.WriteLine("\nThank you for using the OpenAI Connector Tester!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nAn unexpected error occurred: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    private static async Task ProcessPrompt(
        OpenAIConnector connector,
        string prompt,
        float? temperature,
        bool enableSearch
    )
    {
        Console.WriteLine("\nSending prompt to OpenAI...");

        try
        {
            // Create prompt options
            var promptOptions = new PromptOptions
            {
                Temperature = temperature,
                // Include search options if search is enabled
                SearchOptions = enableSearch
                    ? new SearchOptions
                    {
                        MaxResults = 3,
                        IncludeUrls = true,
                        IncludeContent = true,
                    }
                    : null,
            };

            // Send prompt to OpenAI
            var response = await connector.SendPromptAsync(prompt, promptOptions);

            // Display the response
            Console.WriteLine("\n--- Response ---");
            Console.WriteLine(response.Content);
            Console.WriteLine("\n--- Metadata ---");
            Console.WriteLine($"Tokens Used: {response.TokensUsed}");

            // Display function calls if any
            if (response.FunctionCalls != null && response.FunctionCalls.Count > 0)
            {
                Console.WriteLine($"Function Calls: {response.FunctionCalls.Count}");
                foreach (var call in response.FunctionCalls)
                {
                    Console.WriteLine($"  - Function: {call.Name}");
                    Console.WriteLine(
                        $"    Parameters: {JsonSerializer.Serialize(call.Parameters)}"
                    );
                    if (call.Result != null)
                    {
                        Console.WriteLine($"    Result: {JsonSerializer.Serialize(call.Result)}");
                    }
                }
            }
            else
            {
                Console.WriteLine("No function calls made");
            }

            // Transition
            Console.Write("\nPress any key to continue...");
            Console.ReadKey();
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError processing prompt: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
            }
        }
    }

    private static void ShowHelp()
    {
        Console.WriteLine("OpenAI Connector Tester - Help");
        Console.WriteLine("======================================");
        Console.WriteLine("Available command line arguments:");
        Console.WriteLine("  --use-search            Enable search capability");
        Console.WriteLine("  --temperature <number>  Set temperature (0.0-1.0)");
        Console.WriteLine("  --help, -h              Show this help message");
        Console.WriteLine("\nExample:");
        Console.WriteLine("  make test-connector ARGS=\"--use-search --temperature 0.3\"");
    }
}
