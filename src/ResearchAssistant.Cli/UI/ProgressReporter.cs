using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ResearchAssistant.Core.Models;
using TaskState = ResearchAssistant.Core.Models.TaskStatus;

namespace ResearchAssistant.Cli.UI.ProgressReporting;

/// <summary>
/// Reports progress of section writing to the console.
/// Tracks the status of each research task and displays changes.
/// </summary>
internal class ProgressReporter : IDisposable
{
    private readonly ExecutionPlan _plan;
    private readonly Dictionary<string, TaskState> _lastReportedStatus = new();
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the ProgressReporter class.
    /// </summary>
    /// <param name="plan">The execution plan to track progress for</param>
    public ProgressReporter(ExecutionPlan plan)
    {
        _plan = plan;

        // Initialize with current status
        foreach (var task in _plan.ResearchTasks)
        {
            _lastReportedStatus[task.SectionId] = task.Status;
        }
    }

    /// <summary>
    /// Continuously reports progress until cancellation is requested.
    /// Updates the console with the latest status every 2 seconds.
    /// </summary>
    /// <param name="cancellationToken">Token to monitor for cancellation requests</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task ReportProgressAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                ReportProgress();
                await Task.Delay(2000, cancellationToken); // Update every 2 seconds
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in progress reporting: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Reports the current progress of all research tasks.
    /// Displays count of tasks in each state and percentage complete.
    /// </summary>
    private void ReportProgress()
    {
        lock (_lock)
        {
            int completed = 0;
            int inProgress = 0;
            int pending = 0;
            int failed = 0;

            // Count statuses
            foreach (var task in _plan.ResearchTasks)
            {
                switch (task.Status)
                {
                    case TaskState.Completed:
                        completed++;
                        break;
                    case TaskState.InProgress:
                        inProgress++;
                        break;
                    case TaskState.Pending:
                        pending++;
                        break;
                    case TaskState.Failed:
                        failed++;
                        break;
                }

                // Report changes since last check
                if (_lastReportedStatus[task.SectionId] != task.Status)
                {
                    ReportStatusChange(
                        task.SectionName,
                        _lastReportedStatus[task.SectionId],
                        task.Status
                    );
                    _lastReportedStatus[task.SectionId] = task.Status;
                }
            }

            // Report overall progress
            int total = _plan.ResearchTasks.Count;
            double percentComplete = (double)completed / total * 100;

            // Clear current line and write progress
            Console.Write(
                $"\rProgress: {percentComplete:F1}% | Completed: {completed}/{total} | In Progress: {inProgress} | Pending: {pending} | Failed: {failed}"
            );
        }
    }

    /// <summary>
    /// Reports a change in status for a specific section.
    /// </summary>
    /// <param name="sectionName">Name of the section that changed status</param>
    /// <param name="oldStatus">Previous status of the section</param>
    /// <param name="newStatus">New status of the section</param>
    private void ReportStatusChange(string sectionName, TaskState oldStatus, TaskState newStatus)
    {
        // Only report meaningful changes
        if (oldStatus == newStatus)
            return;

        Console.WriteLine();
        Console.ForegroundColor = newStatus switch
        {
            TaskState.Completed => ConsoleColor.Green,
            TaskState.InProgress => ConsoleColor.Cyan,
            TaskState.Failed => ConsoleColor.Red,
            _ => Console.ForegroundColor,
        };

        Console.WriteLine($"Section \"{sectionName}\" is now {newStatus} (was {oldStatus})");
        Console.ResetColor();
    }

    /// <summary>
    /// Disposes of resources used by the ProgressReporter.
    /// </summary>
    public void Dispose()
    {
        // Clean up if needed
    }
}
