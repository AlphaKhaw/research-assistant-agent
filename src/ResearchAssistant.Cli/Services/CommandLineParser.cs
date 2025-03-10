using System;
using System.IO;
using ResearchAssistant.Cli.Models;
using ResearchAssistant.Cli.UI;
using ResearchAssistant.Cli.Utils;

namespace ResearchAssistant.Cli.Services;

/// <summary>
/// Handles parsing and validation of command line arguments
/// </summary>
public static class CommandLineParser
{
    /// <summary>
    /// Parses command-line arguments into a structured options object.
    /// </summary>
    /// <param name="args">Command-line arguments to parse</param>
    /// <returns>A CommandLineOptions object containing the parsed arguments</returns>
    public static CommandLineOptions ParseCommandLineArgs(string[] args)
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

    /// <summary>
    /// Displays help information about available command-line arguments.
    /// </summary>
    public static void ShowHelp()
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

    /// <summary>
    /// Validates that required environment variables are set.
    /// </summary>
    /// <param name="options">Command-line options that may affect which variables are required</param>
    /// <returns>True if all required environment variables are set, false otherwise</returns>
    public static bool ValidateEnvironmentVariables(CommandLineOptions options)
    {
        bool isValid = true;

        var openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrEmpty(openAiApiKey))
        {
            ConsoleUI.PrintError("OPENAI_API_KEY environment variable not set");
            isValid = false;
        }

        if (options.UseSearch)
        {
            var googleApiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
            if (string.IsNullOrEmpty(googleApiKey))
            {
                ConsoleUI.PrintError(
                    "GOOGLE_API_KEY environment variable not set (required for search)"
                );
                isValid = false;
            }

            var googleSearchEngineId = Environment.GetEnvironmentVariable(
                "GOOGLE_SEARCH_ENGINE_ID"
            );
            if (string.IsNullOrEmpty(googleSearchEngineId))
            {
                ConsoleUI.PrintError(
                    "GOOGLE_SEARCH_ENGINE_ID environment variable not set (required for search)"
                );
                isValid = false;
            }
        }

        if (!isValid)
        {
            ConsoleUI.PrintInfo("\nPlease set the required environment variables and try again.");
        }

        return isValid;
    }
}
