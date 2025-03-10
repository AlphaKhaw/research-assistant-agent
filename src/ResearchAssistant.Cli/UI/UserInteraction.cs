using System;
using System.Collections.Generic;
using ResearchAssistant.Cli.UI;

namespace ResearchAssistant.Cli.UI;

/// <summary>
/// Provides methods for user interaction
/// </summary>
public static class UserInteraction
{
    /// <summary>
    /// Asks the user if they want to revise the current plan.
    /// </summary>
    /// <returns>True if the user wants to revise, false otherwise</returns>
    public static bool AskUserForRevision()
    {
        ConsoleUI.PrintPrompt("Do you want to revise this plan? (y/n): ");
        var response = Console.ReadLine()?.Trim().ToLower();
        return response == "y" || response == "yes";
    }

    /// <summary>
    /// Asks the user if they want to proceed with a specified action.
    /// </summary>
    /// <param name="question">The question to ask the user</param>
    /// <returns>True if the user wants to proceed, false otherwise</returns>
    public static bool AskUserToProceed(string question)
    {
        ConsoleUI.PrintPrompt($"{question} (y/n): ");
        var response = Console.ReadLine()?.Trim().ToLower();
        return response == "y" || response == "yes";
    }

    /// <summary>
    /// Gets multiline feedback from the user. Input ends when the user enters a blank line.
    /// </summary>
    /// <returns>The feedback entered by the user</returns>
    public static string GetUserFeedback()
    {
        var lines = new List<string>();
        string line;

        while (!string.IsNullOrWhiteSpace(line = Console.ReadLine()))
        {
            lines.Add(line);
        }

        return string.Join("\n", lines);
    }
}
