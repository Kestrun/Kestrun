using System.Net;

namespace Kestrun.Models;
/// <summary>
/// Options for Content-Disposition header.
/// </summary>
public class ContentDispositionOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContentDispositionOptions"/> class.
    /// </summary>
    public ContentDispositionOptions()
    {
        FileName = null;
        Type = ContentDispositionType.NoContentDisposition;
    }

    /// <summary>
    /// Gets or sets the file name to use in the Content-Disposition header.
    /// </summary>
    public string? FileName { get; set; }
    /// <summary>
    /// Gets or sets the type of Content-Disposition header to use.
    /// </summary>
    public ContentDispositionType Type { get; set; }

    /// <summary>
    /// Returns the Content-Disposition header value as a string, based on the type and file name.
    /// </summary>
    /// <returns>The Content-Disposition header value, or an empty string if no disposition is set.</returns>
    public override string ToString()
    {
        if (Type == ContentDispositionType.NoContentDisposition)
        {
            return string.Empty;
        }

        var disposition = Type == ContentDispositionType.Attachment ? "attachment" : "inline";
        if (string.IsNullOrEmpty(FileName))
        {
            return disposition;
        }

        // Escape the filename to handle special characters
        var escapedFileName = WebUtility.UrlEncode(FileName);
        return $"{disposition}; filename=\"{escapedFileName}\"";
    }
}
