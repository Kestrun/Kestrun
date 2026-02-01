using System.Reflection;

namespace Kestrun.Forms;

/// <summary>
/// Provides helper methods for form handling.
/// </summary>
internal static class FormHelper
{
    /// <summary>
    /// Populates <see cref="KrFormPartRule.NestedRules"/> for rules within a <see cref="KrFormOptions"/> instance.
    /// This uses <see cref="KrFormPartRule.Scope"/> as the parent container rule name and attaches all scoped rules
    /// to their matching container rule(s) (case-insensitive).
    /// </summary>
    /// <param name="options">The options containing rules to link.</param>
    internal static void PopulateNestedRulesFromScopes(KrFormOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Clear first to avoid duplication when called multiple times.
        foreach (var rule in options.Rules)
        {
            rule.NestedRules.Clear();
        }

        // Build a lookup of container-name -> list of container rules.
        var containers = new Dictionary<string, List<KrFormPartRule>>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in options.Rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Name))
            {
                continue;
            }

            if (!containers.TryGetValue(rule.Name, out var bag))
            {
                bag = [];
                containers.Add(rule.Name, bag);
            }

            bag.Add(rule);
        }

        foreach (var child in options.Rules)
        {
            if (string.IsNullOrWhiteSpace(child.Scope))
            {
                continue;
            }

            if (!containers.TryGetValue(child.Scope, out var parents) || parents.Count == 0)
            {
                continue;
            }

            foreach (var parent in parents)
            {
                var exists = false;
                for (var i = 0; i < parent.NestedRules.Count; i++)
                {
                    if (ReferenceEquals(parent.NestedRules[i], child))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    parent.NestedRules.Add(child);
                }
            }
        }
    }

    /// <summary>
    /// Applies KrPartAttribute annotations to the host's form part rules.
    /// </summary>
    /// <param name="host">The Kestrun host runtime.</param>
    /// <param name="p">The property info to inspect for KrPartAttribute annotations.</param>
    /// <param name="scopeName">The parent multipart scope name (null for root).</param>
    internal static void ApplyKrPartAttributes(Hosting.KestrunHost host, PropertyInfo p, string? scopeName)
    {
        var name = ResolvePartName(p);
        // Apply KrPartAttribute annotations
        foreach (var attr in p.GetCustomAttributes<KrPartAttribute>(inherit: false))
        {
            var formPartRule = new KrFormPartRule
            {
                Name = name ?? "",
                Scope = scopeName,
                Description = attr.Description,
                Required = attr.Required,
                AllowMultiple = attr.AllowMultiple,

                MaxBytes = attr.MaxBytes > 0 ? attr.MaxBytes : null,
                DecodeMode = attr.DecodeMode,
                DestinationPath = attr.DestinationPath,
                StoreToDisk = attr.StoreToDisk,
            };

            formPartRule.AllowedContentTypes.AddRange(attr.ContentTypes);
            formPartRule.AllowedExtensions.AddRange(attr.Extensions);
            var added = host.AddFormPartRule(formPartRule);
            if (!added && host.Runtime.FormPartRules.TryGetValue(formPartRule.Name, out var existingRule))
            {
                formPartRule = existingRule;
            }
            AttachNestedRule(host, scopeName, formPartRule);
        }
    }

    /// <summary>
    /// Resolves the part name for a property, including PowerShell dynamic type handling.
    /// </summary>
    /// <param name="p">The property info to inspect.</param>
    /// <returns>The resolved part name.</returns>
    internal static string? ResolvePartName(PropertyInfo p)
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

        return name;
    }

    /// <summary>
    /// Builds form part rules for a CLR type decorated with <see cref="KrPartAttribute"/>.
    /// Nested multipart container parts (parts whose allowed content types include <c>multipart/*</c>)
    /// will have their inner part rules added to <see cref="KrFormPartRule.NestedRules"/>, and the
    /// inner rules will be scoped to the container's part name.
    /// </summary>
    /// <param name="rootType">The root type to inspect.</param>
    /// <returns>A flattened list of rules including nested rules.</returns>
    internal static List<KrFormPartRule> BuildFormPartRulesFromType(Type rootType)
    {
        ArgumentNullException.ThrowIfNull(rootType);
        var visited = new HashSet<Type>();
        return BuildFormPartRulesFromType(rootType, scopeName: null, visited);
    }

    /// <summary>
    /// Builds form part rules for a type, scoped under a given parent container name.
    /// </summary>
    private static List<KrFormPartRule> BuildFormPartRulesFromType(Type type, string? scopeName, HashSet<Type> visited)
    {
        if (!visited.Add(type))
        {
            return [];
        }

        var rules = new List<KrFormPartRule>();
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(static p => p.DeclaringType == p.ReflectedType))
        {
            var attr = prop.GetCustomAttribute<KrPartAttribute>(inherit: false);
            if (attr is null)
            {
                continue;
            }

            var partName = ResolvePartName(prop);
            if (string.IsNullOrWhiteSpace(partName))
            {
                continue;
            }

            var underlying = UnwrapElementType(prop.PropertyType);
            var ruleName = IsMultipartContainerRule(attr) && underlying is not null && IsComplexType(underlying)
                ? underlying.Name
                : partName;

            var rule = new KrFormPartRule
            {
                Name = ruleName,
                Scope = scopeName,
                Description = attr.Description,
                Required = attr.Required,
                AllowMultiple = attr.AllowMultiple,
                MaxBytes = attr.MaxBytes > 0 ? attr.MaxBytes : null,
                DecodeMode = attr.DecodeMode,
                DestinationPath = attr.DestinationPath,
                StoreToDisk = attr.StoreToDisk,
            };

            rule.AllowedContentTypes.AddRange(attr.ContentTypes);
            rule.AllowedExtensions.AddRange(attr.Extensions);

            rules.Add(rule);

            if (!IsMultipartContainerRule(attr) || underlying is null || !IsComplexType(underlying))
            {
                continue;
            }

            var childRules = BuildFormPartRulesFromType(underlying, scopeName: rule.Name, visited);
            foreach (var child in childRules)
            {
                rule.NestedRules.Add(child);
            }

            rules.AddRange(childRules);
        }

        return rules;
    }

    private static Type? UnwrapElementType(Type type)
    {
        var t = type;
        if (t.IsArray)
        {
            t = t.GetElementType()!;
        }

        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            t = Nullable.GetUnderlyingType(t)!;
        }

        return t;
    }

    private static bool IsMultipartContainerRule(KrPartAttribute attr)
    {
        if (attr.ContentTypes is null || attr.ContentTypes.Length == 0)
        {
            return false;
        }

        foreach (var ct in attr.ContentTypes)
        {
            if (ct is null)
            {
                continue;
            }

            if (ct.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsComplexType(Type type) => !type.IsEnum && type != typeof(string) && !type.IsPrimitive;

    /// <summary>
    /// Attaches a rule to its parent container rule when a scope is provided.
    /// </summary>
    /// <param name="host">The Kestrun host runtime.</param>
    /// <param name="scopeName">The parent multipart scope name.</param>
    /// <param name="childRule">The rule to attach.</param>
    private static void AttachNestedRule(Hosting.KestrunHost host, string? scopeName, KrFormPartRule childRule)
    {
        if (string.IsNullOrWhiteSpace(scopeName))
        {
            return;
        }

        if (!host.Runtime.FormPartRules.TryGetValue(scopeName, out var parentRule))
        {
            return;
        }

        for (var i = 0; i < parentRule.NestedRules.Count; i++)
        {
            var existing = parentRule.NestedRules[i];
            if (string.Equals(existing.Name, childRule.Name, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(existing.Scope, childRule.Scope, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        parentRule.NestedRules.Add(childRule);
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
