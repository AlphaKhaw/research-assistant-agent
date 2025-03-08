using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.Functions;
using ResearchAssistant.Core.Interfaces;
using ResearchAssistant.Core.Models;

namespace ResearchAssistant.Core.Services.LLM;

public class OpenAIConnector : IModelConnector
{
    private readonly Kernel _kernel;
    private readonly KernelPlugin _searchPlugin;
    private readonly ILogger<OpenAIConnector> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly LlmOptions _options;

    public OpenAIConnector(
        Kernel kernel,
        KernelPlugin searchPlugin,
        LlmOptions options,
        JsonSerializerOptions jsonOptions,
        ILogger<OpenAIConnector> logger
    )
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _searchPlugin = searchPlugin ?? throw new ArgumentNullException(nameof(searchPlugin));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _jsonOptions = jsonOptions ?? throw new ArgumentNullException(nameof(jsonOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // Public Method: Sends a prompt to OpenAI with function calling enabled to support search
    public async Task<LlmResponse> SendPromptAsync(
        string prompt,
        PromptOptions? promptOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation(
            "Sending prompt to OpenAI with search capability: {PromptLength} chars",
            prompt.Length
        );
        promptOptions ??= new PromptOptions();

        try
        {
            // Ensure search plugin is registered
            if (!_kernel.Plugins.Contains(_searchPlugin))
            {
                _kernel.Plugins.Add(_searchPlugin);
                _logger.LogDebug("Search plugin added to kernel");
            }

            // Configure OpenAI execution settings with function calling
            var settings = new OpenAIPromptExecutionSettings
            {
                Temperature = promptOptions.Temperature ?? _options.DefaultTemperature,
                TopP = promptOptions.TopP ?? _options.DefaultTopP,
                MaxTokens = promptOptions.MaxTokens ?? _options.DefaultMaxTokens,
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
            };

            // Add execution settings to arguments
            var arguments = new KernelArguments(settings);

            // Missing: Add search options if provided
            if (promptOptions.SearchOptions != null)
            {
                arguments["searchOptions"] = promptOptions.SearchOptions;
            }

            // Add custom data to arguments if provided
            if (promptOptions.ContextData != null)
            {
                foreach (var item in promptOptions.ContextData)
                {
                    arguments[item.Key] = item.Value;
                }
            }

            // Add search options if provided
            var result = await _kernel.InvokePromptAsync(
                promptOptions.IncludeCitations
                    ? $"{prompt}\n\nInclude citations to the relevant information where it is referenced in the response."
                    : prompt,
                arguments: arguments,
                cancellationToken: cancellationToken
            );

            // Process function calling metadata if available
            var functionMetadata = new List<FunctionCallInfo>();
            if (result?.Metadata != null && result.Metadata.ContainsKey("FunctionCalls"))
            {
                var functionCalls = result.Metadata["FunctionCalls"];
                _logger.LogInformation(
                    "LLM used function calling: {FunctionCalls}",
                    JsonSerializer.Serialize(functionCalls, _jsonOptions)
                );

                // Process function call metadata here if needed
                // This would extract information about which functions were called
            }

            return new LlmResponse
            {
                Content = result.ToString(),
                TokensUsed = GetTokenCount(result),
                FunctionCalls = functionMetadata,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending prompt to OpenAI: {Message}", ex.Message);
            throw;
        }
    }

    // Private Method: Extracts token count from kernel
    private int GetTokenCount(FunctionResult result)
    {
        try
        {
            if (
                result?.Metadata != null
                && result.Metadata.TryGetValue("Usage", out var usageObj)
                && usageObj is JsonElement usageElement
            )
            {
                if (
                    usageElement.TryGetProperty("TotalTokens", out var tokensElement)
                    && tokensElement.ValueKind == JsonValueKind.Number
                )
                {
                    return tokensElement.GetInt32();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract token count from result metadata");
        }

        return 0;
    }
}
