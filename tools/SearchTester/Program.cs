using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ResearchAssistant.Core.Models;
using ResearchAssistant.Core.Services.Search;

namespace SearchTester;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("======================================");
        Console.WriteLine("Google Search Tool Tester");
        Console.WriteLine("======================================");

        // Parse command line arguments
        int maxResults = 5;
        bool includeUrls = true;
        bool includeContent = true;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--max-results" && i + 1 < args.Length)
            {
                if (int.TryParse(args[i + 1], out int parsedMaxResults))
                {
                    maxResults = parsedMaxResults;
                    i++;
                }
            }
            else if (args[i] == "--no-urls")
            {
                includeUrls = false;
            }
            else if (args[i] == "--no-content")
            {
                includeContent = false;
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

        if (string.IsNullOrEmpty(openAiModel))
        {
            Console.WriteLine("Error: OPENAI_MODEL environment variable not set");
            missingEnvVars = true;
        }

        if (string.IsNullOrEmpty(googleApiKey))
        {
            Console.WriteLine("Error: GOOGLE_API_KEY environment variable not set");
            missingEnvVars = true;
        }

        if (string.IsNullOrEmpty(googleSearchEngineId))
        {
            Console.WriteLine("Error: GOOGLE_SEARCH_ENGINE_ID environment variable not set");
            missingEnvVars = true;
        }

        if (missingEnvVars)
        {
            Console.WriteLine("\nPlease set the required environment variables and try again.");
            Console.WriteLine("Example:");
            Console.WriteLine("  export OPENAI_API_KEY=your_key_here");
            Console.WriteLine("  export OPENAI_MODEL=your_key_here");
            Console.WriteLine("  export GOOGLE_API_KEY=your_key_here");
            Console.WriteLine("  export GOOGLE_SEARCH_ENGINE_ID=your_id_here");
            return;
        }

        try
        {
            // Fix nullable reference - Warning CS8604
            if (openAiApiKey == null || googleApiKey == null || googleSearchEngineId == null)
            {
                Console.WriteLine("ERROR: Required environment variables are missing");
                return;
            }

            // Set up dependency injection
            var services = new ServiceCollection();
            services.AddLogging(builder =>
                builder.AddConsole().SetMinimumLevel(LogLevel.Information)
            );

            // Create and configure the kernel with OpenAI
            var kernelBuilder = Kernel.CreateBuilder();
            kernelBuilder.AddOpenAIChatCompletion(modelId: openAiModel, apiKey: openAiApiKey);

            var kernel = kernelBuilder.Build();

            services.AddSingleton(kernel);
            services.AddSingleton(
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                }
            );

            // Builds the service provider from the service collection
            var provider = services.BuildServiceProvider();
            var logger = provider.GetRequiredService<
                ILogger<GoogleSearchToolWithFunctionCalling>
            >();

            Console.WriteLine("\nInitializing Google Search Tool...");

            // Create the search tool with the collected credentials
            var searchTool = new GoogleSearchToolWithFunctionCalling(
                googleSearchEngineId,
                googleApiKey,
                kernel,
                provider.GetRequiredService<JsonSerializerOptions>(),
                logger
            );

            // Print the current settings
            Console.WriteLine("\nSearch Settings:");
            Console.WriteLine($"- Max Results: {maxResults}");
            Console.WriteLine($"- Include URLs: {includeUrls}");
            Console.WriteLine($"- Include Content: {includeContent}");
            Console.WriteLine("======================================");

            // Interactive search loop
            bool continueSearching = true;

            while (continueSearching)
            {
                Console.WriteLine("\n======================================");
                Console.Write("Enter search query (or 'exit' to quit): ");
                var query = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(query) || query.Trim().ToLower() == "exit")
                {
                    continueSearching = false;
                    continue;
                }

                // Creates SearchOptions object
                var options = new SearchOptions
                {
                    MaxResults = maxResults,
                    IncludeUrls = includeUrls,
                    IncludeContent = includeContent,
                };

                Console.WriteLine(
                    "\nSearching... (this may take a moment while the LLM optimizes your query)"
                );

                try
                {
                    var results = await searchTool.SearchAsync(query, options);

                    Console.WriteLine($"\nFound {results.Count} results:");

                    if (results.Count == 0)
                    {
                        Console.WriteLine("No results found. Try a different query.");
                        continue;
                    }

                    for (int i = 0; i < results.Count; i++)
                    {
                        var result = results[i];
                        Console.WriteLine($"\n[Result {i + 1}]");
                        Console.WriteLine($"Title: {result.Title}");
                        Console.WriteLine($"URL: {result.Url}");

                        if (!string.IsNullOrEmpty(result.Snippet))
                        {
                            // Get a preview of the snippet (first 150 chars)
                            var snippetPreview =
                                result.Snippet.Length <= 150
                                    ? result.Snippet
                                    : result.Snippet.Substring(0, 150) + "...";

                            Console.WriteLine($"Snippet: {snippetPreview}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\nError performing search: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                    }
                    Console.WriteLine("\nStack trace:");
                    Console.WriteLine(ex.StackTrace);
                }
            }

            Console.WriteLine("\nThank you for using the Google Search Tool Tester!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nAn unexpected error occurred: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    private static void ShowHelp()
    {
        Console.WriteLine("Google Search Tool Tester - Help");
        Console.WriteLine("======================================");
        Console.WriteLine("Available command line arguments:");
        Console.WriteLine("  --max-results <number>  Set maximum number of results (default: 5)");
        Console.WriteLine("  --no-urls               Don't include URLs in search results");
        Console.WriteLine("  --no-content            Don't include content in search results");
        Console.WriteLine("  --help, -h              Show this help message");
        Console.WriteLine("\nExample:");
        Console.WriteLine("  make test-search ARGS=\"--max-results 10\"");
    }
}
