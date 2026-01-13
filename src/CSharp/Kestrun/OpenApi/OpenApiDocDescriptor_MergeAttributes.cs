namespace Kestrun.OpenApi;

/// <summary>
/// Generates OpenAPI v2 (Swagger) documents from C# types decorated with OpenApiSchema attributes.
/// </summary>
public partial class OpenApiDocDescriptor
{
    #region Schemas Attribute Merging

    /// <summary>
    /// Merges multiple OpenApiPropertyAttribute instances into one.
    /// </summary>
    /// <param name="attrs">An array of OpenApiPropertyAttribute instances to merge.</param>
    /// <returns>A single OpenApiPropertyAttribute instance representing the merged attributes.</returns>
    private static OpenApiPropertyAttribute? MergeSchemaAttributes(OpenApiPropertyAttribute[] attrs)
    {
        if (attrs == null || attrs.Length == 0)
        {
            return null;
        }

        if (attrs.Length == 1)
        {
            return attrs[0];
        }

        var m = new OpenApiPropertyAttribute();

        foreach (var a in attrs)
        {
            MergeStringProperties(m, a);
            MergeEnumAndCollections(m, a);
            MergeNumericProperties(m, a);
            MergeBooleanProperties(m, a);
            MergeTypeAndRequired(m, a);
            MergeCustomFields(m, a);
        }

        return m;
    }

    /// <summary>
    /// Merges string properties where the last non-empty value wins.
    /// </summary>
    /// <param name="merged">The merged OpenApiPropertyAttribute to update.</param>
    /// <param name="attr">The OpenApiPropertyAttribute to merge from.</param>
    private static void MergeStringProperties(OpenApiPropertyAttribute merged, OpenApiPropertyAttribute attr)
    {
        if (!string.IsNullOrWhiteSpace(attr.Title))
        {
            merged.Title = attr.Title;
        }

        if (!string.IsNullOrWhiteSpace(attr.Description))
        {
            merged.Description = attr.Description;
        }

        if (!string.IsNullOrWhiteSpace(attr.Format))
        {
            merged.Format = attr.Format;
        }

        if (!string.IsNullOrWhiteSpace(attr.Pattern))
        {
            merged.Pattern = attr.Pattern;
        }

        if (!string.IsNullOrWhiteSpace(attr.Maximum))
        {
            merged.Maximum = attr.Maximum;
        }

        if (!string.IsNullOrWhiteSpace(attr.Minimum))
        {
            merged.Minimum = attr.Minimum;
        }
    }

    /// <summary>
    /// Merges enum and collection properties.
    /// </summary>
    /// <param name="merged">The merged OpenApiPropertyAttribute to update.</param>
    /// <param name="attr">The OpenApiPropertyAttribute to merge from.</param>
    private static void MergeEnumAndCollections(OpenApiPropertyAttribute merged, OpenApiPropertyAttribute attr)
    {
        if (attr.Enum is { Length: > 0 })
        {
            merged.Enum = [.. merged.Enum ?? [], .. attr.Enum];
        }

        if (attr.Default is not null)
        {
            merged.Default = attr.Default;
        }

        if (attr.Example is not null)
        {
            merged.Example = attr.Example;
        }
    }

    /// <summary>
    /// Merges numeric properties where values >= 0 are considered explicitly set.
    /// </summary>
    /// <param name="merged">The merged OpenApiPropertyAttribute to update.</param>
    /// <param name="attr">The OpenApiPropertyAttribute to merge from.</param>
    private static void MergeNumericProperties(OpenApiPropertyAttribute merged, OpenApiPropertyAttribute attr)
    {
        if (attr.MaxLength >= 0)
        {
            merged.MaxLength = attr.MaxLength;
        }

        if (attr.MinLength >= 0)
        {
            merged.MinLength = attr.MinLength;
        }

        if (attr.MaxItems >= 0)
        {
            merged.MaxItems = attr.MaxItems;
        }

        if (attr.MinItems >= 0)
        {
            merged.MinItems = attr.MinItems;
        }

        if (attr.MultipleOf is not null)
        {
            merged.MultipleOf = attr.MultipleOf;
        }
    }

    /// <summary>
    /// Merges boolean properties using OR logic.
    /// </summary>
    /// <param name="merged">The merged OpenApiPropertyAttribute to update.</param>
    /// <param name="attr">The OpenApiPropertyAttribute to merge from.</param>
    private static void MergeBooleanProperties(OpenApiPropertyAttribute merged, OpenApiPropertyAttribute attr)
    {
        merged.Nullable |= attr.Nullable;
        merged.ReadOnly |= attr.ReadOnly;
        merged.WriteOnly |= attr.WriteOnly;
        merged.Deprecated |= attr.Deprecated;
        merged.UniqueItems |= attr.UniqueItems;
        merged.ExclusiveMaximum |= attr.ExclusiveMaximum;
        merged.ExclusiveMinimum |= attr.ExclusiveMinimum;
    }

    /// <summary>
    /// Merges type and required properties.
    /// </summary>
    /// <param name="merged">The merged OpenApiPropertyAttribute to update.</param>
    /// <param name="attr">The OpenApiPropertyAttribute to merge from.</param>
    private static void MergeTypeAndRequired(OpenApiPropertyAttribute merged, OpenApiPropertyAttribute attr)
    {
        if (attr.Type != OaSchemaType.None)
        {
            merged.Type = attr.Type;
        }

        if (attr.RequiredProperties is { Length: > 0 })
        {
            merged.RequiredProperties = [.. (merged.RequiredProperties ?? []).Concat(attr.RequiredProperties).Distinct()];
        }
    }

    /// <summary>
    /// Merges custom fields like XmlName.
    /// </summary>
    /// <param name="merged">The merged OpenApiPropertyAttribute to update.</param>
    /// <param name="attr">The OpenApiPropertyAttribute to merge from.</param>
    private static void MergeCustomFields(OpenApiPropertyAttribute merged, OpenApiPropertyAttribute attr)
    {
        if (!string.IsNullOrWhiteSpace(attr.XmlName))
        {
            merged.XmlName = attr.XmlName;
        }
    }
    #endregion
}
