
namespace Kestrun.Models;

/// <summary>
/// Specifies the type of Content-Disposition header to use in the HTTP response.
/// </summary>
public enum ContentDispositionType
{
    /// <summary>
    /// Indicates that the content should be downloaded as an attachment.
    /// </summary>
    Attachment,
    /// <summary>
    /// Indicates that the content should be displayed inline in the browser.
    /// </summary>
    Inline,
    /// <summary>
    /// Indicates that no Content-Disposition header should be set.
    /// </summary>
    NoContentDisposition
}
