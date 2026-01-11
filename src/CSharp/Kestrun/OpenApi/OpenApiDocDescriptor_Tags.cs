using System.Collections;
using Microsoft.OpenApi;

namespace Kestrun.OpenApi;

/// <summary>
/// Helper methods for accessing OpenAPI document components.
/// </summary>
public partial class OpenApiDocDescriptor
{
    /// <summary>
    /// Adds a tag if it doesn't exist and returns the existing or newly created tag.
    /// </summary>
    /// <param name="name">The name of the tag.</param>
    /// <param name="description">Optional description of the tag.</param>
    /// <param name="summary">Optional summary of the tag.</param>
    /// <param name="parent">Optional parent tag name.</param>
    /// <param name="kind">Optional kind of the tag.</param>
    /// <param name="externalDocs">Optional external documentation for the tag.</param>
    /// <param name="extensions">Optional OpenAPI extensions for the tag.</param>
    /// <returns>The existing or newly created OpenApiTag.</returns>
    public OpenApiTag AddTag(
        string name,
        string? description = null,
        string? summary = null,
        string? parent = null,
        string? kind = null,
        OpenApiExternalDocs? externalDocs = null,
        IDictionary? extensions = null
     )
    {
        // Reuse your existing logic (it also ensures the tag is added)
        var tag = GetOrCreateTagItem(name);

        // Optional: update metadata when provided
        if (!string.IsNullOrWhiteSpace(description))
        {
            tag.Description = description;
        }

        if (!string.IsNullOrWhiteSpace(summary))
        {
            tag.Summary = summary;
        }
        if (externalDocs is not null)
        {
            tag.ExternalDocs = externalDocs;
        }
        if (!string.IsNullOrWhiteSpace(parent))
        {
            tag.Parent = new OpenApiTagReference(parent);
        }
        if (!string.IsNullOrWhiteSpace(kind))
        {
            tag.Kind = kind;
        }
        if (extensions is not null && extensions.Count > 0)
        {
            tag.Extensions ??= new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal);

            foreach (DictionaryEntry entry in extensions)
            {
                var rawKey = entry.Key?.ToString();
                if (string.IsNullOrWhiteSpace(rawKey))
                {
                    continue;
                }

                // Enforce OpenAPI extension naming
                var key = rawKey.StartsWith("x-", StringComparison.OrdinalIgnoreCase)
                    ? rawKey
                    : "x-" + rawKey;

                var node = OpenApiJsonNodeFactory.FromObject(entry.Value);

                // If you want to skip null-valued extensions:
                if (node is null)
                {
                    continue;
                }

                tag.Extensions[key] = new JsonNodeExtension(node);
            }
        }

        return tag;
    }

    /// <summary>
    /// Adds a tag only if it doesn't already exist (by comparer). Returns true if added.
    /// </summary>
    private bool AddTagIfMissing(OpenApiTag tag)
    {
        Document.Tags ??= new HashSet<OpenApiTag>();
        return Document.Tags.Add(tag); // HashSet with comparer prevents duplicates
    }
    /// <summary>
    /// Removes a tag by name. Returns true if removed.
    /// </summary>
    private bool RemoveTag(string name)
    {
        if (Document.Tags is null)
        {
            return false;
        }

        // Uses comparer-based removal (fast path for HashSet)
        return Document.Tags.Remove(new OpenApiTag { Name = name });
    }
    /// <summary>
    /// Removes a tag by instance. Returns true if removed.
    /// </summary>
    public bool RemoveTag(OpenApiTag tag) =>
    Document.Tags is not null && Document.Tags.Remove(tag);

    /// <summary>
    /// Gets or creates a tag item in the OpenAPI document by name.
    /// </summary>
    /// <param name="name">The name of the tag to get or create.</param>
    /// <returns>The retrieved or newly created OpenApiTag.</returns>
    private OpenApiTag GetOrCreateTagItem(string name)
    {
        Document.Tags ??= new HashSet<OpenApiTag>();

        var probe = new OpenApiTag { Name = name };

        if (!Document.Tags.Contains(probe))
        {
            _ = Document.Tags.Add(probe);
            return probe;
        }

        return Document.Tags.First(t =>
            string.Equals(t.Name, name, StringComparison.Ordinal));
    }

    /// <summary>
    /// Tries to get a tag item by name from the OpenAPI document.
    /// </summary>
    /// <param name="name"> The name of the tag to retrieve.</param>
    /// <param name="tag"> The retrieved OpenApiTag if found; otherwise, null.</param>
    /// <returns>True if the tag was found; otherwise, false.</returns>
    public bool TryGetTag(string name, out OpenApiTag? tag)
    {
        tag = Document.Tags?.FirstOrDefault(t =>
            string.Equals(t.Name, name, StringComparison.Ordinal));

        return tag is not null;
    }

    /// <summary>
    /// Creates an OpenApiExternalDocs object with optional extensions.
    /// </summary>
    /// <param name="url">The URL for the external documentation.</param>
    /// <param name="description">An optional description of the external documentation.</param>
    /// <param name="extensions">Optional extensions for the external documentation.</param>
    /// <returns>An OpenApiExternalDocs object.</returns>
    /// <exception cref="ArgumentException">Thrown when the URL is null, empty, or whitespace.</exception>
    public static OpenApiExternalDocs CreateExternalDocs(
        Uri url,
        string? description = null,
        IDictionary? extensions = null)
    {
        var docs = new OpenApiExternalDocs
        {
            Url = url,
            Description = description
        };

        if (extensions is not null && extensions.Count > 0)
        {
            docs.Extensions = new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal);

            foreach (DictionaryEntry entry in extensions)
            {
                var rawKey = entry.Key?.ToString();
                if (string.IsNullOrWhiteSpace(rawKey))
                {
                    continue;
                }

                // Enforce OpenAPI extension naming
                var key = rawKey.StartsWith("x-", StringComparison.OrdinalIgnoreCase)
                    ? rawKey
                    : "x-" + rawKey;

                var node = OpenApiJsonNodeFactory.FromObject(entry.Value);
                if (node is null)
                {
                    continue;
                }

                docs.Extensions[key] = new JsonNodeExtension(node);
            }
        }

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
    public static OpenApiExternalDocs CreateExternalDocs(
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
}
