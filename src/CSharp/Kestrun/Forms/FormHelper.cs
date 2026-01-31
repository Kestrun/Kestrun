using System.Reflection;

namespace Kestrun.Forms;

/// <summary>
/// Provides helper methods for form handling.
/// </summary>
internal static class FormHelper
{
    /// <summary>
    /// Applies KrPartAttribute annotations to the host's form part rules.
    /// </summary>
    /// <param name="host">The Kestrun host runtime.</param>
    /// <param name="p">The property info to inspect for KrPartAttribute annotations.</param>
    /// <param name="built">The set of already built types to avoid recursion.</param>
    internal static void ApplyKrPartAttributes(Hosting.KestrunHost host, PropertyInfo p, HashSet<Type> built)
    {
        var name = p.Name;
        var type = p.PropertyType;

        // Arrays / collections → unwrap
        if (type.IsArray)
        {
            type = type.GetElementType()!;
        }
        // Nullable<T> → unwrap
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            type = Nullable.GetUnderlyingType(type)!;
        }

        var asm = type.Assembly;

        // PowerShell classes are emitted into a dynamic assembly
        if (asm.IsDynamic)
        {
            // Stable PowerShell assembly naming convention
            var fullName = asm.FullName;
            if (fullName != null &&
                  fullName.StartsWith("PowerShell Class Assembly", StringComparison.Ordinal))
            {
                // PropertyType.FullName can be null for some dynamic types; avoid dereference
                var propFullName = p.PropertyType.FullName;
                if (propFullName is not null)
                {
                    name = propFullName.Trim('[', ']');
                }
            }
        }
        // Apply KrPartAttribute annotations
        foreach (var attr in p.GetCustomAttributes<KrPartAttribute>(inherit: false))
        {
            var parent = built.First();
            var formPartRule = new KrFormPartRule
            {
                Name = name ?? "",
                Scope = parent.FullName,
                Description = attr.Description,
                Required = attr.Required,
                AllowMultiple = attr.AllowMultiple,

                MaxBytes = attr.MaxBytes,
                DecodeMode = attr.DecodeMode,
                DestinationPath = attr.DestinationPath,
                StoreToDisk = attr.StoreToDisk,
            };

            formPartRule.AllowedContentTypes.AddRange(attr.ContentTypes);
            formPartRule.AllowedExtensions.AddRange(attr.Extensions);
            if (name is not null)
            {
                foreach (var existingRule in host.Runtime.FormPartRules.Where(r => r.Value.Scope == parent.FullName))
                {
                    formPartRule.NestedRules.Add(existingRule.Value);
                }
            }
            _ = host.AddFormPartRule(formPartRule);
        }
    }

    /// <summary>
    /// Applies KrBindFormAttribute to form options.
    /// </summary>
    /// <param name="attr">The KrBindFormAttribute instance to apply.</param>
    /// <returns>A KrFormOptions object configured based on the attribute.</returns>
    internal static KrFormOptions ApplyKrPartAttributes(KrBindFormAttribute attr)
    {
        var formOptions = new KrFormOptions
        {
            ComputeSha256 = attr.ComputeSha256,
            EnablePartDecompression = attr.EnablePartDecompression,
            RejectUnknownRequestContentType = attr.RejectUnknownRequestContentType,
            RejectUnknownContentEncoding = attr.RejectUnknownContentEncoding
        };

        if (attr.DefaultUploadPath is not null)
        {
            formOptions.DefaultUploadPath = attr.DefaultUploadPath;
        }

        if (attr.MaxDecompressedBytesPerPart > 0)
        {
            formOptions.MaxDecompressedBytesPerPart = attr.MaxDecompressedBytesPerPart;
        }

        if (attr.AllowedPartContentEncodings is not null)
        {
            formOptions.AllowedPartContentEncodings.Clear();
            formOptions.AllowedPartContentEncodings.AddRange(attr.AllowedPartContentEncodings);
        }
        if (attr.MaxRequestBodyBytes > 0)
        {
            formOptions.Limits.MaxRequestBodyBytes = attr.MaxRequestBodyBytes;
        }
        if (attr.MaxPartBodyBytes > 0)
        {
            formOptions.Limits.MaxPartBodyBytes = attr.MaxPartBodyBytes;
        }
        if (attr.MaxParts > 0)
        {
            formOptions.Limits.MaxParts = attr.MaxParts;
        }
        if (attr.MaxHeaderBytesPerPart > 0)
        {
            formOptions.Limits.MaxHeaderBytesPerPart = attr.MaxHeaderBytesPerPart;
        }
        if (attr.MaxFieldValueBytes > 0)
        {
            formOptions.Limits.MaxFieldValueBytes = attr.MaxFieldValueBytes;
        }
        if (attr.MaxNestingDepth > 0)
        {
            formOptions.Limits.MaxNestingDepth = attr.MaxNestingDepth;
        }
        return formOptions;
    }
}
