
namespace Kestrun.Tasks;

/// <summary>
/// Represents the progress state of a task.
/// </summary>
public class ProgressiveKestrunTaskState
{
    // Synchronizes concurrent updates coming from different runspaces.
#if NET9_0_OR_GREATER
    private readonly Lock _syncRoot = new();
#else
    private readonly object _syncRoot = new();
#endif
    private int _percentComplete = 0;
    private string _statusMessage = "Not started";

    /// <summary>
    /// Percentage of task completed (0-100).
    /// </summary>
    public int PercentComplete
    {
        get
        {
            lock (_syncRoot)
            {
                return _percentComplete;
            }
        }

        set => SetState(value, StatusMessage, nameof(value));
    }

    /// <summary>
    /// Optional status message for the current progress.
    /// </summary>
    public string StatusMessage
    {
        get
        {
            lock (_syncRoot)
            {
                return _statusMessage;
            }
        }
        set => SetState(PercentComplete, value ?? throw new ArgumentNullException(nameof(value)));
    }
    /// <summary>
    /// Returns a string representation of the progress state.
    /// </summary>
    /// <returns> A string representation of the progress state. </returns>
    public override string ToString()
    {
        lock (_syncRoot)
        {
            return $"{_percentComplete}% - {_statusMessage}";
        }
    }

    /// <summary>
    /// Resets the progress state to initial values.
    /// </summary>
    /// <param name="message"> Optional message to include with the reset status.</param>
    public void Reset(string message = "Not started") => SetState(0, message);
    /// <summary>
    /// Marks the progress as complete with an optional message.
    /// </summary>
    /// <param name="message"> Optional message to include with the completion status.</param>
    public void Complete(string message = "Completed") => SetState(100, message);

    /// <summary>
    /// Marks the progress as failed with an optional message.
    /// </summary>
    /// <param name="message"> Optional message to include with the failure status.</param>
    public void Fail(string message = "Failed") => SetState(100, message);

    /// <summary>
    /// Marks the progress as cancelled with an optional message.
    /// </summary>
    /// <param name="message"> Optional message to include with the cancellation status.</param>
    public void Cancel(string message = "Cancelled") => SetState(100, message);

    private void SetState(int percentComplete, string statusMessage, string percentParameterName = nameof(PercentComplete))
    {
        if (string.IsNullOrWhiteSpace(statusMessage))
        {
            throw new ArgumentNullException(nameof(statusMessage));
        }

        if (percentComplete < 0)
        {
            percentComplete = 0;
        }
        else if (percentComplete > 100)
        {
            percentComplete = 100;
        }


        lock (_syncRoot)
        {
            _percentComplete = percentComplete;
            _statusMessage = statusMessage;
        }
    }
}
