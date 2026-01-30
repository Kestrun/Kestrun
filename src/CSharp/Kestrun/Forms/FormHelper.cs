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
        foreach (var attr in p.GetCustomAttributes<KrPartAttribute>(inherit: false))
        {
            var parent = built.First();
            var formPartRule = new KrFormPartRule
            {
                Name = parent.FullName + "." + p.Name,
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
            _ = host.AddFormPartRule(formPartRule);
        }
    }
}
