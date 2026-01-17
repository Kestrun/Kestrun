using System.Collections;
using Microsoft.OpenApi;

namespace Kestrun.OpenApi;

/// <summary>
/// Helper methods for accessing OpenAPI document components.
/// </summary>
public partial class OpenApiDocDescriptor
{
    /// <summary>
    /// Creates an OpenApiExternalDocs object with optional extensions.
    /// </summary>
    /// <param name="url">The URL for the external documentation.</param>
    /// <param name="description">An optional description of the external documentation.</param>
    /// <param name="extensions">Optional extensions for the external documentation.</param>
    /// <returns>An OpenApiExternalDocs object.</returns>
    /// <exception cref="ArgumentException">Thrown when the URL is null, empty, or whitespace.</exception>
    public OpenApiExternalDocs CreateExternalDocs(
        Uri url,
        string? description = null,
        IDictionary? extensions = null)
    {
        var docs = new OpenApiExternalDocs
        {
            Url = url,
            Description = description,
            Extensions = BuildExtensions(extensions)
        };

        return docs;
    }
    /// <summary>
    /// Creates an OpenApiExternalDocs object from a URL string with optional extensions.
    /// </summary>
    /// <param name="url">The URL for the external documentation.</param>
    /// <param name="description">An optional description of the external documentation.</param>
    /// <param name="extensions">Optional extensions for the external documentation.</param>
    /// <returns>An OpenApiExternalDocs object.</returns>
    /// <exception cref="ArgumentException">Thrown when the URL is null, empty, or whitespace.</exception>
    public OpenApiExternalDocs CreateExternalDocs(
            string url,
            string? description = null,
            IDictionary? extensions = null)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("ExternalDocs url is required.", nameof(url));
        }
        // Reuse the other overload
        return CreateExternalDocs(new Uri(url, UriKind.RelativeOrAbsolute), description, extensions);
    }

    /// <summary>
    /// Creates an OpenApiContact object with optional extensions.
    /// </summary>
    /// <param name="name">The name of the contact person or organization.</param>
    /// <param name="url">The URL of the contact person or organization.</param>
    /// <param name="email">The email address of the contact person or organization.</param>
    /// <param name="extensions">Optional extensions for the contact information.</param>
    /// <returns>An OpenApiContact object.</returns>
    public OpenApiContact CreateInfoContact(
            string? name = null,
            Uri? url = null,
            string? email = null,
            IDictionary? extensions = null)
    {
        var contact = new OpenApiContact
        {
            Extensions = BuildExtensions(extensions)
        };

        if (url != null)
        {
            contact.Url = url;
        }

        if (!string.IsNullOrEmpty(name))
        {
            contact.Name = name;
        }

        if (!string.IsNullOrEmpty(email))
        {
            contact.Email = email;
        }

        return contact;
    }
}
