using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ResearchAssistant.Core.Interfaces;
using ResearchAssistant.Core.Models;

namespace ResearchAssistant.Core.Services;

public class SectionWriter : ISectionWriter
{
    private readonly IModelConnector _modelConnector;
    private readonly ISearchTool _searchTool;
    private readonly ILogger<SectionWriter> _logger;
    private readonly LlmOptions _llmOptions;

    private const string SECTION_WRITING_PROMPT =
        @"
    <Instructions>
    Write a comprehensive section for a research report based on the provided information.
    </Instructions>
    
    <ReportTopic>
    {topic}
    </ReportTopic>
    
    <Section>
    Name: {sectionName}
    Description: {sectionDescription}
    </Section>
    
    <ResearchInformation>
    {researchInformation}
    </ResearchInformation>
    
    <AdjacentSections>
    {adjacentSections}
    </AdjacentSections>
    
    <WritingGuidelines>
    - Write with academic rigor but maintain readability
    - Include relevant examples to illustrate key points
    - Cite sources using [Source X] format where X is the source number
    - Maintain a neutral, informative tone
    - Target approximately 500-800 words for this section
    </WritingGuidelines>";

    private const string INTRODUCTION_REVISION_PROMPT =
        @"
    <Instructions>
    Revise this introduction to better reflect the complete content of the report.
    </Instructions>
    
    <ReportTopic>
    {topic}
    </ReportTopic>

    <CurrentIntroduction>
    {currentIntroduction}
    </CurrentIntroduction>

    <CompletedSections>
    {completedSectionsSummary}
    </CompletedSections>

    <RevisionGuidelines>
    - Ensure the introduction properly previews all key topics covered in the report
    - Maintain a clear thesis or purpose statement
    - Provide a roadmap for what readers can expect in each section
    - Keep the tone consistent with the rest of the report
    - Don't expand beyond 500 words
    </RevisionGuidelines>";

    public SectionWriter(
        IModelConnector modelConnector,
        ISearchTool searchTool,
        LlmOptions llmOptions,
        ILogger<SectionWriter> logger
    )
    {
        _modelConnector = modelConnector ?? throw new ArgumentNullException(nameof(modelConnector));
        _searchTool = searchTool ?? throw new ArgumentNullException(nameof(searchTool));
        _llmOptions = llmOptions ?? throw new ArgumentNullException(nameof(llmOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Write all sections of a report based on the execution plan
    /// </summary>
    public async Task<Report> WriteReportAsync(
        ExecutionPlan plan,
        CancellationToken cancellationToken = default
    )
    {
        if (plan == null)
            throw new ArgumentNullException(nameof(plan));

        _logger.LogInformation("Starting report writing process for topic: {Topic}", plan.Topic);

        // Update execution plan status
        plan.Status = ExecutionStatus.InProgress;
        plan.StartedAt = DateTime.UtcNow;

        // Phase 1: Process introduction (draft) and body sections
        var initialAndBodyTasks = plan
            .ResearchTasks.Where(t => GetSectionPhase(t, plan) != ExecutionPhase.Final)
            .ToList();

        _logger.LogInformation(
            "Processing {Count} initial/body sections",
            initialAndBodyTasks.Count
        );
        await ProcessSectionsInParallel(initialAndBodyTasks, plan, cancellationToken);

        // Phase 2: Process conclusion and finalize introduction
        var finalSections = plan
            .ResearchTasks.Where(t => GetSectionPhase(t, plan) == ExecutionPhase.Final)
            .ToList();

        _logger.LogInformation("Processing {Count} final sections", finalSections.Count);

        // Find introduction to revise
        var introSection = plan.ResearchTasks.FirstOrDefault(t =>
            GetSectionPhase(t, plan) == ExecutionPhase.Initial
        );

        if (introSection != null)
        {
            _logger.LogInformation("Revising introduction based on completed body sections");
            await ReviseIntroductionWithCompletedBody(introSection, plan, cancellationToken);
        }

        await ProcessSectionsInParallel(finalSections, plan, cancellationToken);

        // Compile the final report
        var report = CompileReport(plan);

        // Update execution plan status
        plan.Status = ExecutionStatus.Completed;
        plan.CompletedAt = DateTime.UtcNow;

        _logger.LogInformation("Report writing completed for topic: {Topic}", plan.Topic);

        return report;
    }

    /// <summary>
    /// Process multiple sections in parallel with a concurrency limit
    /// </summary>
    private async Task ProcessSectionsInParallel(
        List<SectionResearchTask> sections,
        ExecutionPlan plan,
        CancellationToken cancellationToken
    )
    {
        // Use SemaphoreSlim to control concurrency
        using var semaphore = new SemaphoreSlim(plan.MaxConcurrentSections);
        var tasks = new List<Task>();

        foreach (var section in sections)
        {
            await semaphore.WaitAsync(cancellationToken);

            tasks.Add(
                Task.Run(
                    async () =>
                    {
                        try
                        {
                            await ProcessSectionAsync(section, plan, cancellationToken);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    },
                    cancellationToken
                )
            );
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Process a single section: research if needed and write content
    /// </summary>
    private async Task ProcessSectionAsync(
        SectionResearchTask section,
        ExecutionPlan plan,
        CancellationToken cancellationToken
    )
    {
        try
        {
            _logger.LogInformation("Processing section: {SectionName}", section.SectionName);
            section.Status = Models.TaskStatus.InProgress;

            // Get corresponding section details from plan
            var sectionDetails = plan.ApprovedPlan.Sections.FirstOrDefault(s =>
                s.Id == section.SectionId
            );
            if (sectionDetails == null)
            {
                _logger.LogError(
                    "Section details not found for ID: {SectionId}",
                    section.SectionId
                );
                section.Status = Models.TaskStatus.Failed;
                return;
            }

            // Perform research if required
            if (sectionDetails.RequiresResearch)
            {
                await PerformResearchAsync(section, plan.Topic, cancellationToken);
            }

            // Generate section content
            var content = await WriteSectionContentAsync(section, plan, cancellationToken);
            section.Content = content;
            section.Status = Models.TaskStatus.Completed;
            section.CompletedAt = DateTime.UtcNow;

            _logger.LogInformation("Completed section: {SectionName}", section.SectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing section {SectionName}: {Message}",
                section.SectionName,
                ex.Message
            );
            section.Status = Models.TaskStatus.Failed;
            section.ErrorMessage = ex.Message;
        }
    }

    /// <summary>
    /// Perform web research for a section
    /// </summary>
    private async Task PerformResearchAsync(
        SectionResearchTask section,
        string topic,
        CancellationToken cancellationToken
    )
    {
        _logger.LogInformation(
            "Performing research for section: {SectionName}",
            section.SectionName
        );

        // Generate search queries based on section details
        var searchQueries = await GenerateSearchQueriesAsync(
            section.SectionName,
            section.Description,
            topic,
            section.MaxSearchQueries,
            cancellationToken
        );

        _logger.LogInformation(
            "Generated {Count} search queries for section: {SectionName}",
            searchQueries.Count,
            section.SectionName
        );

        // Execute each search query
        foreach (var query in searchQueries)
        {
            try
            {
                _logger.LogDebug("Executing search query: {Query}", query);

                // Get results directly as List<SearchResult>
                var searchResults = await _searchTool.SearchAsync(query, null, cancellationToken);

                if (searchResults.Any())
                {
                    section.SearchResults.Add(
                        new SearchResultSet
                        {
                            Query = query,
                            Results = searchResults, // Use directly
                            Timestamp = DateTime.UtcNow,
                        }
                    );

                    _logger.LogInformation(
                        "Found {Count} results for query: {Query}",
                        searchResults.Count,
                        query
                    );
                }
                else
                {
                    _logger.LogWarning("No results found for query: {Query}", query);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error during search for query {Query}: {Message}",
                    query,
                    ex.Message
                );
            }
        }
    }

    /// <summary>
    /// Generate search queries for a section
    /// </summary>
    private async Task<List<string>> GenerateSearchQueriesAsync(
        string sectionName,
        string sectionDescription,
        string topic,
        int maxQueries,
        CancellationToken cancellationToken
    )
    {
        var prompt =
            $"Generate {maxQueries} specific and focused search queries for researching:\n"
            + $"Topic: {topic}\n"
            + $"Section: {sectionName}\n"
            + $"Description: {sectionDescription}\n\n"
            + "Make each query specific enough to return targeted, relevant information. "
            + "Format as a numbered list with each query on a separate line.";

        var options = new PromptOptions { Temperature = 0.3f, MaxTokens = 500 };

        var response = await _modelConnector.SendPromptAsync(prompt, options, cancellationToken);

        // Parse the numbered lines into a list
        var queries = new List<string>();
        var lines = response.Content.Split('\n');

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine))
                continue;

            // Match numbered or bullet point lines
            if (
                (
                    trimmedLine.Length > 2
                    && char.IsDigit(trimmedLine[0])
                    && (trimmedLine[1] == '.' || trimmedLine[1] == ')')
                    && char.IsWhiteSpace(trimmedLine[2])
                ) || trimmedLine.StartsWith("- ")
            )
            {
                var query = trimmedLine.Substring(trimmedLine.IndexOf(' ') + 1).Trim();
                if (!string.IsNullOrEmpty(query))
                {
                    queries.Add(query);
                }
            }
        }

        return queries.Take(maxQueries).ToList();
    }

    /// <summary>
    /// Write content for a section based on research and section details
    /// </summary>
    private async Task<string> WriteSectionContentAsync(
        SectionResearchTask section,
        ExecutionPlan plan,
        CancellationToken cancellationToken
    )
    {
        // Gather research information
        var researchInfo = new StringBuilder();
        foreach (var searchResultSet in section.SearchResults)
        {
            researchInfo.AppendLine($"Query: {searchResultSet.Query}");

            for (int i = 0; i < searchResultSet.Results.Count; i++)
            {
                var result = searchResultSet.Results[i];
                researchInfo.AppendLine($"Source {i + 1}: {result.Title}");
                researchInfo.AppendLine($"URL: {result.Url}");
                researchInfo.AppendLine($"Content: {result.Snippet}");
                researchInfo.AppendLine();
            }
        }

        // Get adjacent sections for context
        var adjacentSections = GetAdjacentSectionInfo(section.SectionId, plan);

        // Prepare the writing prompt
        var prompt = SECTION_WRITING_PROMPT
            .Replace("{topic}", plan.Topic)
            .Replace("{sectionName}", section.SectionName)
            .Replace("{sectionDescription}", section.Description)
            .Replace("{researchInformation}", researchInfo.ToString())
            .Replace("{adjacentSections}", adjacentSections);

        var options = new PromptOptions
        {
            Temperature = 0.7f, // Higher temperature for creative writing
            MaxTokens = 2500, // Allow for substantial content
            IncludeCitations = true,
        };

        var response = await _modelConnector.SendPromptAsync(prompt, options, cancellationToken);
        return response.Content;
    }

    /// <summary>
    /// Revise the introduction after body sections are completed
    /// </summary>
    private async Task ReviseIntroductionWithCompletedBody(
        SectionResearchTask introSection,
        ExecutionPlan plan,
        CancellationToken cancellationToken
    )
    {
        // Gather summaries of all completed sections
        var completedSections = new StringBuilder();
        foreach (
            var task in plan.ResearchTasks.Where(t =>
                t.Status == Models.TaskStatus.Completed && t.SectionId != introSection.SectionId
            )
        )
        {
            completedSections.AppendLine($"Section: {task.SectionName}");
            completedSections.AppendLine($"Description: {task.Description}");

            // Add a brief summary (first paragraph) of each section
            var firstParagraph = task
                .Content.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();

            if (!string.IsNullOrEmpty(firstParagraph))
            {
                completedSections.AppendLine($"Summary: {firstParagraph}");
            }
            completedSections.AppendLine();
        }

        // Prepare the revision prompt
        var prompt = INTRODUCTION_REVISION_PROMPT
            .Replace("{topic}", plan.Topic)
            .Replace("{currentIntroduction}", introSection.Content)
            .Replace("{completedSectionsSummary}", completedSections.ToString());

        var options = new PromptOptions { Temperature = 0.5f, MaxTokens = 1500 };

        var response = await _modelConnector.SendPromptAsync(prompt, options, cancellationToken);

        // Update the introduction with the revised content
        introSection.Content = response.Content;
        introSection.IsRevised = true;

        _logger.LogInformation("Introduction revised to reflect complete report content");
    }

    /// <summary>
    /// Get information about sections adjacent to this one for context
    /// </summary>
    private string GetAdjacentSectionInfo(string sectionId, ExecutionPlan plan)
    {
        var sections = plan.ApprovedPlan.Sections;
        int currentIndex = sections.FindIndex(s => s.Id == sectionId);

        if (currentIndex < 0)
            return "No adjacent section information available.";

        var result = new StringBuilder();

        // Add previous section info if it exists
        if (currentIndex > 0)
        {
            var prevSection = sections[currentIndex - 1];
            result.AppendLine("Previous section:");
            result.AppendLine($"Name: {prevSection.Name}");
            result.AppendLine($"Description: {prevSection.Description}");
            result.AppendLine();
        }

        // Add next section info if it exists
        if (currentIndex < sections.Count - 1)
        {
            var nextSection = sections[currentIndex + 1];
            result.AppendLine("Next section:");
            result.AppendLine($"Name: {nextSection.Name}");
            result.AppendLine($"Description: {nextSection.Description}");
        }

        return result.ToString();
    }

    /// <summary>
    /// Determine the execution phase of a section
    /// </summary>
    private ExecutionPhase GetSectionPhase(SectionResearchTask task, ExecutionPlan plan)
    {
        var section = plan.ApprovedPlan.Sections.FirstOrDefault(s => s.Id == task.SectionId);
        return section?.ExecutionPhase ?? ExecutionPhase.Body;
    }

    /// <summary>
    /// Compile all sections into a complete report
    /// </summary>
    private Report CompileReport(ExecutionPlan plan)
    {
        _logger.LogInformation("Compiling final report for topic: {Topic}", plan.Topic);

        var sections = new List<ReportContent>();
        var citations = new List<Citation>();
        int citationCount = 0;

        // Order sections by their number in the original plan
        foreach (var planSection in plan.ApprovedPlan.Sections.OrderBy(s => s.Number))
        {
            var task = plan.ResearchTasks.FirstOrDefault(t => t.SectionId == planSection.Id);

            // Skip sections that weren't completed
            if (task?.Status != Models.TaskStatus.Completed)
                continue;

            // Extract citations from the content
            var (processedContent, extractedCitations) = ExtractCitations(
                task.Content,
                citationCount
            );

            sections.Add(
                new ReportContent
                {
                    SectionId = planSection.Id,
                    SectionNumber = planSection.Number,
                    SectionName = planSection.Name,
                    Content = processedContent,
                    IsRevised = task.IsRevised,
                }
            );

            citations.AddRange(extractedCitations);
            citationCount += extractedCitations.Count;
        }

        return new Report
        {
            Id = Guid.NewGuid().ToString(),
            Topic = plan.Topic,
            Sections = sections,
            Citations = citations,
            CreatedAt = DateTime.UtcNow,
            PlanId = plan.PlanId,
            TokensUsed = plan.ResearchTasks.Sum(t => t.TokensUsed),
        };
    }

    /// <summary>
    /// Extract citations from content and return processed content and citation list
    /// </summary>
    private (string processedContent, List<Citation> citations) ExtractCitations(
        string content,
        int startIndex
    )
    {
        var citations = new List<Citation>();
        var processedContent = content;

        // Simple citation extraction - this could be enhanced with regex for more complex formats
        // Looking for patterns like [Source X]
        int sourceIndex = startIndex;
        while (processedContent.Contains("[Source "))
        {
            int start = processedContent.IndexOf("[Source ");
            int end = processedContent.IndexOf("]", start);

            if (end > start)
            {
                string citationText = processedContent.Substring(start, end - start + 1);

                // Extract URL if it exists in the research results
                // This is a simplified placeholder - real implementation would match to actual sources
                string url = $"https://example.com/source-{sourceIndex + 1}";
                string title = $"Source {sourceIndex + 1}";

                citations.Add(
                    new Citation
                    {
                        Id = Guid.NewGuid().ToString(),
                        Number = sourceIndex + 1,
                        Text = citationText,
                        Url = url,
                        Title = title,
                    }
                );

                // Replace citation with a numbered format
                processedContent = processedContent.Replace(citationText, $"[{sourceIndex + 1}]");
                sourceIndex++;
            }
            else
            {
                break;
            }
        }

        return (processedContent, citations);
    }
}
