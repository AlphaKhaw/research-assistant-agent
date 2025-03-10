using System;
using System.IO;
using System.Threading.Tasks;
using ResearchAssistant.Core.Models;

namespace ResearchAssistant.Cli.Utils;

/// <summary>
/// Provides file operations for the application
/// </summary>
public static class FileOperations
{
    /// <summary>
    /// Saves the generated report to a Markdown file.
    /// </summary>
    /// <param name="report">The report to save</param>
    /// <param name="topic">The research topic (used for the filename)</param>
    /// <returns>The file path where the report was saved</returns>
    public static async Task<string> SaveReportToFile(Report report, string topic)
    {
        // Create a valid filename from the topic
        string sanitizedTopic = string.Join("_", topic.Split(Path.GetInvalidFileNameChars()));
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string filename = $"research_report_{sanitizedTopic}_{timestamp}.md";
        string filePath = Path.Combine(Environment.CurrentDirectory, filename);

        using var writer = new StreamWriter(filePath);

        // Write header
        await writer.WriteLineAsync($"# {report.Topic}");
        await writer.WriteLineAsync($"\nGenerated on {report.CreatedAt.ToLocalTime():f}\n");

        // Write table of contents
        await writer.WriteLineAsync("## Table of Contents");
        foreach (var section in report.Sections.OrderBy(s => s.SectionNumber))
        {
            await writer.WriteLineAsync(
                $"{section.SectionNumber}. [{section.SectionName}](#{section.SectionName.Replace(" ", "-").ToLower()})"
            );
        }
        await writer.WriteLineAsync();

        // Write each section
        foreach (var section in report.Sections.OrderBy(s => s.SectionNumber))
        {
            await writer.WriteLineAsync($"## {section.SectionNumber}. {section.SectionName}");
            await writer.WriteLineAsync(section.Content);
            await writer.WriteLineAsync();
        }

        // Write citations if available
        if (report.Citations != null && report.Citations.Any())
        {
            await writer.WriteLineAsync("## References");

            foreach (var citation in report.Citations.OrderBy(c => c.Number))
            {
                await writer.WriteLineAsync(
                    $"[{citation.Number}] {citation.Title}. {citation.Url}"
                );
            }
        }

        return filePath;
    }
}
