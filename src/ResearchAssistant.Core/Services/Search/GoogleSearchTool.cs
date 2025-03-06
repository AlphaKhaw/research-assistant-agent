using System; // Basic C# infrastructure (exceptions, math functions)
using System.Collections.Generic; // For List<SearchResult> and other collection types
using System.Text.Json; // For JSON serialization/deserialization options
using System.Threading; // For CancellationToken support
using System.Threading.Tasks; // For async Task support
using Microsoft.Extensions.Logging; // For structured logging (ILogger)
using Microsoft.SemanticKernel; // Core Semantic Kernel (Kernel, KernelArguments, etc.)
using Microsoft.SemanticKernel.Data; // For search-related data structures (TextSearchOptions, TextSearchFilter)
using Microsoft.SemanticKernel.Plugins.Web.Google; // Google Search plugin (GoogleTextSearch class)
using ResearchAssistant.Core.Interfaces; // For ISearchTool interface
using ResearchAssistant.Core.Models; // For SearchResult and SearchOptions models

namespace ResearchAssistant.Core.Services.Search;

public class GoogleSearchToolWithFunctionCalling : ISearchTool
{
    // Core Google search service from Semantic Kernel
    private readonly GoogleTextSearch _googlesearch;

    // Semantic Kernel instance for LLM operations
    private readonly Kernel _kernel;

    // JSON serialization settings with camelCase property names
    private readonly JsonSerializerOptions _jsonOptions;

    // Plugin name constant to avoid string literals
    private const string SEARCH_PLUGIN_NAME = "SearchPlugin";

    // Plugin instance (created once and reused for efficiency)
    private KernelPlugin? _searchPlugin;

    public GoogleSearchToolWithFunctionCalling(
        string searchEngineId,
        string apiKey,
        Kernel kernel,
        JsonSerializerOptions jsonOptions,
        ILogger<GoogleSearchToolWithFunctionCalling> logger
    )
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(searchEngineId))
        {
            throw new ArgumentException(
                "Google Search Engine ID cannot be empty",
                nameof(searchEngineId)
            );
        }

        if (string.IsNullOrEmpty(apiKey))
        {
            throw new ArgumentException("Google Search API Key cannot be empty", nameof(apiKey));
        }

        _googleSearch = new GoogleTextSearch(searchEngineId, apiKey);
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jsonOptions = jsonOptions ?? throw new ArgumentNullException(nameof(jsonOptions));
    }

    // Public Method: Implementation of SearchAsync method from ISearchTool interface
    public async Task<List<SearchResult>> SearchAsync(
        string query,
        SearchOptions options,
        CancellationToken cancellationToken = default
    )
    {
        // Validate input query
        if (string.IsNullOrWhiteSpace(query))
        {
            return new List<SearchResult>();
        }

        _logger.LogInformation("Preparing search with function calling for: {Query}", query);

        try
        {
            // Lazy-initialize the search plugin only once
            if (_searchPlugin == null)
            {
                _searchPlugin = _googleSearch.CreateWithGetTextSearchResults(SEARCH_PLUGIN_NAME);
                _kernel.Plugins.Add(_searchPlugin);
                _logger.LogDebug("Search plugin created and added to kernel");
            }

            // Generate optimiszed search terms using LLM
            var optimisedTerms = await GetOptimizedSearchTermsAsync(query, cancellationToken);

            // Configure search options
            var textSearchOptions = CreateSearchOptions(options);

            // Execute the search
            return await ExecuteSearchAsync(
                optimizedTerms,
                textSearchOptions,
                options,
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing Google search for query: {Query}", query);
            return new List<SearchResult>();
        }
    }

    // Private Method: Uses LLM to optimise search terms
    private async Task<string> GetOptimizedSearchTermsAsync(
        string query,
        CancellationToken cancellationToken
    )
    {
        // This prompt instructs the LLM to analyze the query and extract the best search terms
        var promptTemplate =
            @"
        Analyze this query and determine the best search terms to find relevant information: 

        Query: {{$query}}

        Think step by step about what search terms would yield the most relevant results.
        Provide ONLY the search terms, not explanations.
        ";

        // Set up arguments for the prompt
        var arguments = new KernelArguments { ["query"] = query };

        // Use the kernel to invoke the LLM with our prompt
        var searchTerms = await _kernel.InvokePromptAsync(
            promptTemplate,
            arguments,
            cancellationToken: cancellationToken
        );

        // Clean up the search terms (remove quotes, trim whitespace, etc.)
        var cleanedTerms = searchTerms.Trim('"', ' ', '\n');

        _logger.LogInformation("Optimized search terms: {SearchTerms}", cleanedTerms);

        // Fall back to original query if LLM returned empty or too short results
        return !string.IsNullOrWhiteSpace(cleanedTerms) && cleanedTerms.Length > 3
            ? cleanedTerms
            : query;
    }

    // Private Method: Create TextSearchOptions from SearchOptions
    private TextSearchOptions CreateSearchOptions(SearchOptions options)
    {
        var textSearchOptions = new TextSearchOptions
        {
            Top = Math.Max(1, options.MaxResults), // Ensure at least 1 result
            Skip = 0,
        };

        // Initialize filter as needed
        TextSearchFilter? filter = null;

        // --- Site filtering ---

        // Apply site filtering based on options
        if (!string.IsNullOrWhiteSpace(options.SiteFilter))
        {
            textSearchOptions.SiteSearch = options.SiteFilter;

            // Apply site search filter mode if specified
            if (
                !string.IsNullOrWhiteSpace(options.SiteFilterMode)
                && (options.SiteFilterMode == "i" || options.SiteFilterMode == "e")
            )
            {
                filter = (filter ?? new TextSearchFilter()).Equality(
                    "siteSearchFilter",
                    options.SiteFilterMode
                );
            }
        }

        // Link relationships
        if (!string.IsNullOrWhiteSpace(options.LinkSite))
        {
            filter = (filter ?? new TextSearchFilter()).Equality("linkSite", options.LinkSite);
        }

        // --- Content filtering ---

        // Date restrictions
        if (!string.IsNullOrWhiteSpace(options.DateRestrict))
        {
            filter = (filter ?? new TextSearchFilter()).Equality(
                "dateRestrict",
                options.DateRestrict
            );
        }

        // Term filtering
        if (!string.IsNullOrWhiteSpace(options.ExactTerms))
        {
            filter = (filter ?? new TextSearchFilter()).Equality("exactTerms", options.ExactTerms);
        }

        if (!string.IsNullOrWhiteSpace(options.ExcludeTerms))
        {
            filter = (filter ?? new TextSearchFilter()).Equality(
                "excludeTerms",
                options.ExcludeTerms
            );
        }

        if (!string.IsNullOrWhiteSpace(options.OrTerms))
        {
            filter = (filter ?? new TextSearchFilter()).Equality("orTerms", options.OrTerms);
        }

        if (!string.IsNullOrWhiteSpace(options.FileType))
        {
            filter = (filter ?? new TextSearchFilter()).Equality("fileType", options.FileType);
        }

        // --- Language and region ---

        if (!string.IsNullOrWhiteSpace(options.Language))
        {
            filter = (filter ?? new TextSearchFilter()).Equality("lr", options.Language);
        }

        if (!string.IsNullOrWhiteSpace(options.Country))
        {
            filter = (filter ?? new TextSearchFilter()).Equality("cr", options.Country);
        }

        // --- Rights and safety ---

        if (!string.IsNullOrWhiteSpace(options.Rights))
        {
            filter = (filter ?? new TextSearchFilter()).Equality("rights", options.Rights);
        }

        if (!string.IsNullOrWhiteSpace(options.SafeSearch))
        {
            filter = (filter ?? new TextSearchFilter()).Equality("safe", options.SafeSearch);
        }

        // --- Duplicate filtering ---

        if (!string.IsNullOrWhiteSpace(options.DuplicateContentFilter))
        {
            filter = (filter ?? new TextSearchFilter()).Equality(
                "filter",
                options.DuplicateContentFilter
            );
        }

        // Apply the constructed filter if any conditions were set
        if (filter != null)
        {
            textSearchOptions.Filter = filter;
        }
        // Handle URL exclusion if no other filters are set but URLs should be excluded
        else if (!options.IncludeUrls)
        {
            // Create empty filter if URLs shouldn't be included
            textSearchOptions.Filter = new TextSearchFilter();
        }

        return textSearchOptions;
    }

    // Private Method: Executes search and maps results
    private async Task<List<SearchResult>> ExecuteSearchAsync(
        string searchTerms,
        TextSearchOptions textSearchOptions,
        SearchOptions options,
        CancellationToken cancellationToken
    )
    {
        // Get results from Google Search
        var kernelResults = await _googlesearch.GetTextSearchResultsAsync(
            searchTerms,
            textSearchOptions,
            cancellationToken
        );
        var searchResults = new List<SearchResult>();

        // Map Google Search results to application's SearchResult model
        await foreach (var item in kernelResults.Results.WithCancellation(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            searchResults.Add(
                new SearchResult
                {
                    Title = item.Name ?? string.Empty,
                    Url = item.Link ?? string.Empty,
                    Snippet = item.Value ?? string.Empty,
                    Content = options.IncludeContent ? item.Value ?? string.Empty : string.Empty,
                }
            );
        }

        _logger.LogInformation("Retrieved {Count} search results", searchResults.Count);
        return searchResults;
    }
}
