using System;
using ResearchAssistant.Core.Models;

namespace ResearchAssistant.Cli.UI;

public static class ConsoleUI
{
    /// <summary>
    /// Displays the research plan to the console.
    /// </summary>
    /// <param name="plan">The research plan to display</param>
    public static void DisplayPlan(ReportPlan plan)
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

    /// <summary>
    /// Displays the execution plan to the console.
    /// </summary>
    /// <param name="plan">The execution plan to display</param>
    public static void DisplayExecutionPlan(ExecutionPlan plan)
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

    /// <summary>
    /// Displays the generated report to the console.
    /// Shows a preview of each section rather than the full content.
    /// </summary>
    /// <param name="report">The report to display</param>
    public static void DisplayReport(Report report)
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

    /// <summary>
    /// Prints a header with specified text and color to the console.
    /// </summary>
    /// <param name="text">Text to display in the header</param>
    /// <param name="color">Color to use for the header text</param>
    public static void PrintHeader(string text, ConsoleColor color)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 50));
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ResetColor();
        Console.WriteLine(new string('=', 50));
    }

    /// <summary>
    /// Prints a stage name header to indicate progression to a new stage in the process.
    /// </summary>
    /// <param name="stageName">Name of the stage to display</param>
    public static void PrintStage(string stageName)
    {
        Console.WriteLine();
        Console.WriteLine(new string('-', 50));
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($">>> {stageName} <<<");
        Console.ResetColor();
        Console.WriteLine(new string('-', 50));
    }

    /// <summary>
    /// Prints an informational message to the console.
    /// </summary>
    /// <param name="message">Message to display</param>
    public static void PrintInfo(string message)
    {
        Console.WriteLine(message);
    }

    /// <summary>
    /// Prints a success message with a green checkmark.
    /// </summary>
    /// <param name="message">Success message to display</param>
    public static void PrintSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✓ {message}");
        Console.ResetColor();
    }

    /// <summary>
    /// Prints a warning message with a yellow warning symbol.
    /// </summary>
    /// <param name="message">Warning message to display</param>
    public static void PrintWarning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"⚠ {message}");
        Console.ResetColor();
    }

    /// <summary>
    /// Prints an error message with a red X symbol.
    /// </summary>
    /// <param name="message">Error message to display</param>
    public static void PrintError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"❌ {message}");
        Console.ResetColor();
    }

    /// <summary>
    /// Prints a prompt for user input with a specific color.
    /// </summary>
    /// <param name="prompt">Prompt text to display</param>
    public static void PrintPrompt(string prompt)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write(prompt);
        Console.ResetColor();
    }

    /// <summary>
    /// Prints statistics about the generated report.
    /// </summary>
    /// <param name="report">The generated report</param>
    /// <param name="plan">The execution plan used to generate the report</param>
    public static void PrintStats(Report report, ExecutionPlan plan)
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

    /// <summary>
    /// Counts the number of words in a text string.
    /// </summary>
    /// <param name="text">The text to count words in</param>
    /// <returns>The number of words in the text</returns>
    public static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        return text.Split(
            new[] { ' ', '\t', '\n', '\r' },
            StringSplitOptions.RemoveEmptyEntries
        ).Length;
    }
}
