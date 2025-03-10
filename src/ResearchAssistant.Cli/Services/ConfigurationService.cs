using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ResearchAssistant.Cli.Models;
using ResearchAssistant.Core.Interfaces;
using ResearchAssistant.Core.Models;
using ResearchAssistant.Core.Services;
using ResearchAssistant.Core.Services.LLM;
using ResearchAssistant.Core.Services.Search;

namespace ResearchAssistant.Cli.Services;

/// <summary>
/// Configures dependency injection services for the application.
/// Sets up logging, API clients, plugins, and core services.
/// </summary>
/// <param name="options">Command-line options that affect service configuration</param>
/// <returns>A ServiceCollection with all required services registered</returns>
public static class ConfigurationService
{
    public static ServiceCollection ConfigureServices(CommandLineOptions options)
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
}
