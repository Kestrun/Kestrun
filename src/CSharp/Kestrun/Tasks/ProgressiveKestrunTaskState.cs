namespace Kestrun.Tasks;

/// <summary>
/// Represents the progress state of a task.
/// </summary>
public class ProgressiveKestrunTaskState
{
    private int _percentComplete = 0;
    private string _statusMessage = "Not started";

    /// <summary>
    /// Percentage of task completed (0-100).
    /// </summary>
    public int PercentComplete
    {
        get => _percentComplete;
        set
        {
            if (value is < 0 or > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "PercentComplete must be between 0 and 100.");
            }
            _percentComplete = value;
        }
    }

    /// <summary>
    /// Optional status message for the current progress.
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set => _statusMessage = value ?? throw new ArgumentNullException(nameof(value));
    }
    /// <summary>
    /// Returns a string representation of the progress state.
    /// </summary>
    /// <returns> A string representation of the progress state. </returns>
    public override string ToString() => $"{PercentComplete}% - {StatusMessage}";

    /// <summary>
    /// Resets the progress state to initial values.
    /// </summary>
    /// <param name="message"> Optional message to include with the reset status.</param>
    public void Reset(string message = "Not started")
    {
        PercentComplete = 0;
        StatusMessage = message;
    }
    /// <summary>
    /// Marks the progress as complete with an optional message.
    /// </summary>
    /// <param name="message"> Optional message to include with the completion status.</param>
    public void Complete(string message = "Completed")
    {
        PercentComplete = 100;
        StatusMessage = message;
    }

    /// <summary>
    /// Marks the progress as failed with an optional message.
    /// </summary>
    /// <param name="message"> Optional message to include with the failure status.</param>
    public void Fail(string message = "Failed")
    {
        PercentComplete = 100;
        StatusMessage = message;
    }

    /// <summary>
    /// Marks the progress as cancelled with an optional message.
    /// </summary>
    /// <param name="message"> Optional message to include with the cancellation status.</param>
    public void Cancel(string message = "Cancelled")
    {
        PercentComplete = 100;
        StatusMessage = message;
    }
}
