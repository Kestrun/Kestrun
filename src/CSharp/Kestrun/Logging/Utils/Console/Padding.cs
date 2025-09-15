namespace Kestrun.Logging.Utils.Console;

/// <summary>
/// Represents padding values for left and right sides, and provides methods to generate padding strings.
/// </summary>
public class Padding
{
    /// <summary>
    /// Gets or sets the padding value for the right side.
    /// </summary>
    public int Right { get; set; }
    /// <summary>
    /// Gets or sets the padding value for the left side.
    /// </summary>
    public int Left { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Padding"/> class with the same padding value for both left and right sides.
    /// </summary>
    /// <param name="all">The padding value to apply to both left and right sides.</param>
    public Padding(int all) => Right = Left = all;

    /// <summary>
    /// Initializes a new instance of the <see cref="Padding"/> class with specified right and left padding values.
    /// </summary>
    /// <param name="right">The padding value for the right side.</param>
    /// <param name="left">The padding value for the left side.</param>
    public Padding(int right, int left)
    {
        Right = right;
        Left = left;
    }

    /// <summary>
    /// Returns a string consisting of spaces for the right padding.
    /// </summary>
    public string RightString() => PadString(Right);

    /// <summary>
    /// Returns a string consisting of spaces for the left padding.
    /// </summary>
    public string LeftString() => PadString(Left);

    private static string PadString(int padding) => string.Concat(Enumerable.Repeat(' ', padding));
}
