using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using ResearchAssistant.Core.Interfaces;
using ResearchAssistant.Core.Models;

namespace ResearchAssistant.Core.Services;

public class PlannerService : IPlanner
{
    private readonly IModelConnector _modelConnector;
    private readonly ILogger<PlannerService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly LlmOptions _llmOptions;

    // Prompt templates
    private const string REPORT_ORGANISATION =
        @"Introduction:
    
    Section 1: 
    Literature Review
    
    Section 2:
    Methodology
    
    Section 3:
    Findings

    Section 4:
    Discussion
    
    Conclusion:
    
    ";
    private const string REPORT_PLANNER_INSTRUCTIONS =
        @"I want a plan for a report that is concise and focused.

    <Report topic>
    The topic of the report is:
    {topic}
    </Report topic>

    <Report organization>
    The report should follow this organization: 
    {report_organization}
    </Report organization>

    <Context>
    Here is context to use to plan the sections of the report: 
    {context}
    </Context>

    <Task>
    Generate a list of sections for the report. Your plan should be tight and focused with NO overlapping sections or unnecessary filler. 

    For example, a good report structure might look like:
    1/ intro
    2/ overview of topic A
    3/ overview of topic B
    4/ comparison between A and B
    5/ conclusion

    Each section should have the fields:

    - Name - Name for this section of the report.
    - Description - Brief overview of the main topics covered in this section.
    - Research - Whether to perform web research for this section of the report.
    - Content - The content of the section, which you will leave blank for now.

    Integration guidelines:
    - Include examples and implementation details within main topic sections, not as separate sections
    - Ensure each section has a distinct purpose with no content overlap
    - Combine related concepts rather than separating them

    Before submitting, review your structure to ensure it has no redundant sections and follows a logical flow.
    </Task>

    <Feedback>
    Here is feedback on the report structure from review (if any):
    {feedback}
    </Feedback>";

    public PlannerService(
        IModelConnector modelConnector,
        LlmOptions llmOptions,
        JsonSerializerOptions jsonOptions,
        ILogger<PlannerService> logger
    )
    {
        _modelConnector = modelConnector ?? throw new ArgumentNullException(nameof(modelConnector));
        _llmOptions = llmOptions ?? throw new ArgumentNullException(nameof(llmOptions));
        _jsonOptions = jsonOptions ?? throw new ArgumentNullException(nameof(jsonOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Generates an initial plan for a report based on a topic and optional organization preferences
    /// </summary>
    public async Task<ReportPlan> GenerateInitialPlanAsync(
        string topic,
        string? organization = REPORT_ORGANISATION,
        string context = "",
        PromptOptions? promptOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("Generating initial plan for topic: {Topic}", topic);

        // Create the prompt with the provided inputs
        var prompt = REPORT_PLANNER_INSTRUCTIONS
            .Replace("{topic}", topic)
            .Replace("{report_organization}", organization)
            .Replace(
                "{context}",
                string.IsNullOrEmpty(context) ? "No additional context provided." : context
            )
            .Replace("{feedback}", "No feedback available for initial plan.");

        // Configure options for planning (lower temperature for more deterministic output)
        var options =
            promptOptions
            ?? new PromptOptions
            {
                Temperature = 0.2f, // Low temperature for more deterministic/structured output
                MaxTokens = 2048, // Ensure enough tokens for a comprehensive plan
                IncludeCitations = false,
                SearchOptions = new SearchOptions
                {
                    MaxResults = 3,
                    IncludeUrls = true,
                    IncludeContent = true,
                },
            };

        // LLM Interaction
        try
        {
            // Send the prompt to the LLM
            var response = await _modelConnector.SendPromptAsync(
                prompt,
                options,
                cancellationToken
            );

            // Log the raw LLM response
            _logger.LogDebug("Raw LLM response for plan generation:\n{Response}", response.Content);

            // Parse the response into a structured ReportPlan
            var plan = ParsePlanFromResponse(response.Content, topic);
            plan.TokensUsed = response.TokensUsed;

            _logger.LogInformation(
                "Successfully generated plan with {SectionCount} sections",
                plan.Sections?.Count ?? 0
            );

            foreach (var section in plan.Sections ?? new List<ReportSection>())
            {
                _logger.LogDebug(
                    "Plan section {Number}: {Name} (Research: {RequiresResearch})",
                    section.Number,
                    section.Name,
                    section.RequiresResearch
                );
            }

            return plan;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating initial plan: {Message}", ex.Message);
            throw new PlanningException("Failed to generate initial report plan", ex);
        }
    }

    /// <summary>
    /// Revises an existing plan based on human feedback
    /// </summary>
    public async Task<ReportPlan> ReviseWithFeedbackAsync(
        ReportPlan existingPlan,
        string feedback,
        PromptOptions? promptOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        if (existingPlan == null)
            throw new ArgumentNullException(nameof(existingPlan));

        if (string.IsNullOrWhiteSpace(feedback))
            throw new ArgumentException("Feedback cannot be empty", nameof(feedback));

        _logger.LogInformation(
            "Revising plan with feedback for topic: {Topic}",
            existingPlan.Topic
        );

        // Create context from the existing plan
        var context =
            $"Previous plan sections: {JsonSerializer.Serialize(existingPlan.Sections, _jsonOptions)}";

        // Create the prompt with the provided inputs
        var prompt = REPORT_PLANNER_INSTRUCTIONS
            .Replace("{topic}", existingPlan.Topic)
            .Replace("{report_organization}", existingPlan.Organization ?? REPORT_ORGANISATION)
            .Replace("{context}", context)
            .Replace("{feedback}", feedback);

        // Configure options for planning (low temperature for more deterministic output)
        var options =
            promptOptions
            ?? new PromptOptions
            {
                Temperature = 0.4f, // Slightly higher than initial to allow for creativity in addressing feedback
                MaxTokens = 2048,
                IncludeCitations = false,
            };

        // LLM Interaction
        try
        {
            // Send the prompt to the LLM
            var response = await _modelConnector.SendPromptAsync(
                prompt,
                options,
                cancellationToken
            );

            // Log the raw LLM response
            _logger.LogDebug("Raw LLM response for plan revision:\n{Response}", response.Content);

            // Parse the response into a structured ReportPlan
            var revisedPlan = ParsePlanFromResponse(response.Content, existingPlan.Topic);
            revisedPlan.TokensUsed = response.TokensUsed;
            revisedPlan.RevisionHistory = new List<PlanRevision>(
                existingPlan.RevisionHistory ?? new List<PlanRevision>()
            )
            {
                new PlanRevision
                {
                    Timestamp = DateTime.UtcNow,
                    Feedback = feedback,
                    PreviousSections = existingPlan.Sections,
                },
            };

            _logger.LogInformation(
                "Successfully revised plan with {SectionCount} sections based on feedback",
                revisedPlan.Sections?.Count ?? 0
            );
            _logger.LogDebug("Applied feedback: {Feedback}", feedback);

            foreach (var section in revisedPlan.Sections ?? new List<ReportSection>())
            {
                _logger.LogDebug(
                    "Revised section {Number}: {Name} (Research: {RequiresResearch})",
                    section.Number,
                    section.Name,
                    section.RequiresResearch
                );
            }

            return revisedPlan;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revising plan with feedback: {Message}", ex.Message);
            throw new PlanningException("Failed to revise report plan", ex);
        }
    }

    /// <summary>
    /// Prepares a plan for execution by initializing research tasks for each section
    /// </summary>
    public Task<ExecutionPlan> PrepareForExecutionAsync(
        ReportPlan approvedPlan,
        ExecutionOptions? executionOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        if (approvedPlan == null)
            throw new ArgumentNullException(nameof(approvedPlan));

        _logger.LogInformation(
            "Preparing execution plan for approved plan: {Topic}",
            approvedPlan.Topic
        );

        // Set default execution options if none provided
        executionOptions ??= new ExecutionOptions
        {
            MaxConcurrentSections = 3,
            MaxSearchQueriesPerSection = 3,
            IncludeReflection = true,
        };

        // Create research tasks for each section
        var researchTasks = new List<SectionResearchTask>();
        if (approvedPlan.Sections != null)
        {
            foreach (var section in approvedPlan.Sections)
            {
                if (section.RequiresResearch)
                {
                    researchTasks.Add(
                        new SectionResearchTask
                        {
                            SectionId = section.Id,
                            SectionName = section.Name,
                            Description = section.Description,
                            Status = Models.TaskStatus.Pending,
                            MaxSearchQueries = executionOptions.MaxSearchQueriesPerSection,
                            SearchResults = new List<SearchResultSet>(),
                            Content = string.Empty,
                        }
                    );

                    _logger.LogDebug(
                        "Added research task for section: {SectionName}",
                        section.Name
                    );
                }
                else
                {
                    _logger.LogDebug(
                        "Section does not require research: {SectionName}",
                        section.Name
                    );
                }
            }
        }

        var executionPlan = new ExecutionPlan
        {
            PlanId = Guid.NewGuid().ToString(),
            Topic = approvedPlan.Topic,
            ApprovedPlan = approvedPlan,
            ResearchTasks = researchTasks,
            MaxConcurrentSections = executionOptions.MaxConcurrentSections,
            Status = ExecutionStatus.Ready,
            CreatedAt = DateTime.UtcNow,
        };

        _logger.LogInformation(
            "Created execution plan with {TaskCount} research tasks",
            researchTasks.Count
        );

        return Task.FromResult(executionPlan);
    }

    /// <summary>
    /// Parse the LLM response into a structured ReportPlan
    /// </summary>
    private ReportPlan ParsePlanFromResponse(string response, string topic)
    {
        try
        {
            // Initial basic parsing logic - this could be enhanced with more sophisticated extraction
            // or by asking the LLM to return a structured JSON format
            var sections = new List<ReportSection>();
            var lines = response.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            ReportSection currentSection = null;
            string currentProperty = null;
            var sectionCount = 0;

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                // Skip empty lines and headers
                if (
                    string.IsNullOrWhiteSpace(trimmedLine)
                    || trimmedLine.StartsWith('#')
                    || trimmedLine.StartsWith("Section")
                    || trimmedLine.Contains("report structure")
                    || trimmedLine.Contains("Here's a plan")
                )
                {
                    continue;
                }

                // Detect numbered section
                if (
                    trimmedLine.Length > 2
                    && char.IsDigit(trimmedLine[0])
                    && (trimmedLine[1] == '.' || trimmedLine[1] == '/')
                    && char.IsWhiteSpace(trimmedLine[2])
                )
                {
                    _logger.LogTrace("Detected new section: {Line}", trimmedLine);

                    // If we already have a section in progress, add it to the list
                    if (currentSection != null)
                    {
                        sections.Add(currentSection);
                    }

                    // Start a new section
                    var sectionName = trimmedLine.Substring(2).Trim();
                    sectionCount++;

                    currentSection = new ReportSection
                    {
                        Id = Guid.NewGuid().ToString(),
                        Number = sectionCount,
                        Name = sectionName,
                        Description = string.Empty,
                        RequiresResearch = true, // Default to true, will be refined below
                    };

                    currentProperty = null;
                    continue;
                }

                // Detect property markers
                if (trimmedLine.StartsWith("- Name:", StringComparison.OrdinalIgnoreCase))
                {
                    currentProperty = "Name";
                    var value = trimmedLine.Substring("- Name:".Length).Trim();
                    if (!string.IsNullOrEmpty(value) && currentSection != null)
                    {
                        currentSection.Name = value;
                    }
                    continue;
                }
                else if (
                    trimmedLine.StartsWith("- Description:", StringComparison.OrdinalIgnoreCase)
                )
                {
                    currentProperty = "Description";
                    var value = trimmedLine.Substring("- Description:".Length).Trim();
                    if (currentSection != null)
                    {
                        currentSection.Description = value;
                    }
                    continue;
                }
                else if (trimmedLine.StartsWith("- Research:", StringComparison.OrdinalIgnoreCase))
                {
                    currentProperty = "Research";
                    var value = trimmedLine.Substring("- Research:".Length).Trim();
                    if (currentSection != null)
                    {
                        currentSection.RequiresResearch =
                            value.Equals("true", StringComparison.OrdinalIgnoreCase)
                            || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
                    }
                    continue;
                }
                else if (trimmedLine.StartsWith("- Content:", StringComparison.OrdinalIgnoreCase))
                {
                    currentProperty = "Content";
                    continue;
                }

                // If we're currently processing a property and have a section, append the line
                if (currentSection != null && currentProperty != null)
                {
                    switch (currentProperty)
                    {
                        case "Name":
                            currentSection.Name = string.IsNullOrEmpty(currentSection.Name)
                                ? trimmedLine
                                : $"{currentSection.Name} {trimmedLine}";
                            break;
                        case "Description":
                            currentSection.Description = string.IsNullOrEmpty(
                                currentSection.Description
                            )
                                ? trimmedLine
                                : $"{currentSection.Description} {trimmedLine}";
                            break;
                        case "Research":
                            // Research is a boolean, typically not multi-line
                            break;
                        case "Content":
                            // Content should be blank at this stage
                            break;
                    }
                }
            }

            // Add the last section if one is in progress
            if (currentSection != null)
            {
                sections.Add(currentSection);
            }

            _logger.LogDebug(
                "Successfully parsed {Count} sections from LLM response",
                sections.Count
            );
            return new ReportPlan
            {
                Id = Guid.NewGuid().ToString(),
                Topic = topic,
                CreatedAt = DateTime.UtcNow,
                Sections = sections,
                RevisionHistory = new List<PlanRevision>(),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing plan from LLM response");
            throw new PlanningException("Failed to parse report plan from LLM response", ex);
        }
    }
}

/// <summary>
/// Custom exception for planning-related errors
/// </summary>
public class PlanningException : Exception
{
    public PlanningException(string message)
        : base(message) { }

    public PlanningException(string message, Exception innerException)
        : base(message, innerException) { }
}
