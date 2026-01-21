using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Kestrun.Hosting;
using Kestrun.Scripting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.VisualBasic;

namespace Kestrun.Certificates;

/// <summary>
/// Compiles C# or VB.NET code into a TLS client certificate validation callback.
/// </summary>
/// <remarks>
/// This is intended for advanced scenarios where a pure .NET delegate is required (e.g. Kestrel TLS handshake callbacks).
/// The compiled delegate executes inside the Kestrel TLS handshake path, so it must be fast and thread-safe.
/// </remarks>
public static class ClientCertificateValidationCompiler
{
    private static readonly ConcurrentDictionary<string, Lazy<Func<X509Certificate2, X509Chain, SslPolicyErrors, bool>>> Cache = new(StringComparer.Ordinal);

    /// <summary>
    /// Compiles code into a TLS client certificate validation callback.
    /// </summary>
    /// <param name="host">The Kestrun host (used for logging).</param>
    /// <param name="code">
    /// The code that forms the body of a method returning <c>bool</c>.
    /// The method signature is:
    /// <c>bool Validate(X509Certificate2 certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)</c>.
    /// </param>
    /// <param name="language">The language used for <paramref name="code"/>.</param>
    /// <returns>A compiled callback delegate.</returns>
    public static Func<X509Certificate2, X509Chain, SslPolicyErrors, bool> Compile(
        KestrunHost host,
        string code,
        ScriptLanguage language = ScriptLanguage.CSharp)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentNullException(nameof(code), "Client certificate validation code cannot be null or whitespace.");
        }

        var cacheKey = BuildCacheKey(language, code);
        var lazy = Cache.GetOrAdd(cacheKey, _ => new Lazy<Func<X509Certificate2, X509Chain, SslPolicyErrors, bool>>(
            () => CompileCore(host, code, language), isThreadSafe: true));

        return lazy.Value;
    }

    private static string BuildCacheKey(ScriptLanguage language, string code)
        => ((int)language).ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + code;

    private static Func<X509Certificate2, X509Chain, SslPolicyErrors, bool> CompileCore(
        KestrunHost host,
        string code,
        ScriptLanguage language)
    {
        return language switch
        {
            ScriptLanguage.CSharp => CompileCSharp(host, code),
            ScriptLanguage.VBNet => CompileVbNet(host, code),
            _ => throw new NotSupportedException($"ClientCertificateValidation supports only CSharp and VBNet, not {language}.")
        };
    }

    private static Func<X509Certificate2, X509Chain, SslPolicyErrors, bool> CompileCSharp(KestrunHost host, string code)
    {
        var source = WrapCSharp(code);
        var startLine = GetStartLine(source, "// ---- User code starts here ----");

        var parseOptions = new CSharpParseOptions(Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp12);
        var tree = CSharpSyntaxTree.ParseText(source, parseOptions);

        var refs = BuildMetadataReferences(includeVisualBasicRuntime: false);
        var compilation = CSharpCompilation.Create(
            assemblyName: $"TlsClientCertValidation_{Guid.NewGuid():N}",
            syntaxTrees: [tree],
            references: refs,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var emit = compilation.Emit(ms);
        ThrowIfErrors(emit.Diagnostics, startLine, host, languageLabel: "C#");

        ms.Position = 0;
        return LoadCallbackDelegate(ms.ToArray(), typeName: "ClientCertValidationScript", methodName: "Validate");
    }

    private static Func<X509Certificate2, X509Chain, SslPolicyErrors, bool> CompileVbNet(KestrunHost host, string code)
    {
        var source = WrapVbNet(code);
        var startLine = GetStartLine(source, "' ---- User code starts here ----");

        var parseOptions = new VisualBasicParseOptions(Microsoft.CodeAnalysis.VisualBasic.LanguageVersion.VisualBasic16_9);
        var tree = VisualBasicSyntaxTree.ParseText(source, parseOptions);

        var refs = BuildMetadataReferences(includeVisualBasicRuntime: true);
        var compilation = VisualBasicCompilation.Create(
            assemblyName: $"TlsClientCertValidation_{Guid.NewGuid():N}",
            syntaxTrees: [tree],
            references: refs,
            options: new VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var emit = compilation.Emit(ms);
        ThrowIfErrors(emit.Diagnostics, startLine, host, languageLabel: "VB.NET");

        ms.Position = 0;
        return LoadCallbackDelegate(ms.ToArray(), typeName: "ClientCertValidationScript", methodName: "Validate");
    }

    private static IEnumerable<MetadataReference> BuildMetadataReferences(bool includeVisualBasicRuntime)
    {
        // Baseline references (includes X509 and many common assemblies)
        var baseRefs = DelegateBuilder.BuildBaselineReferences();

        // Ensure the assembly containing SslPolicyErrors is referenced.
        var netSecurityAsm = typeof(SslPolicyErrors).Assembly;
        var netSecurityRef = string.IsNullOrWhiteSpace(netSecurityAsm.Location)
            ? null
            : MetadataReference.CreateFromFile(netSecurityAsm.Location);

        // Add already-loaded assemblies to improve binding for "using" namespaces.
        var loaded = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && SafeHasLocation(a))
            .Select(a => MetadataReference.CreateFromFile(a.Location));

        IEnumerable<MetadataReference> refs = baseRefs;
        if (netSecurityRef is not null)
        {
            refs = refs.Append(netSecurityRef);
        }

        refs = refs.Concat(loaded);

        if (includeVisualBasicRuntime)
        {
            refs = refs.Append(MetadataReference.CreateFromFile(typeof(Microsoft.VisualBasic.Constants).Assembly.Location));
        }

        return refs;
    }

    private static bool SafeHasLocation(Assembly a)
    {
        try
        {
            var loc = a.Location;
            return !string.IsNullOrEmpty(loc) && File.Exists(loc);
        }
        catch
        {
            return false;
        }
    }

    private static Func<X509Certificate2, X509Chain, SslPolicyErrors, bool> LoadCallbackDelegate(byte[] asmBytes, string typeName, string methodName)
    {
        var asm = Assembly.Load(asmBytes);
        var method = asm.GetType(typeName, throwOnError: true)!
            .GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)
            ?? throw new MissingMethodException(typeName, methodName);

        return (Func<X509Certificate2, X509Chain, SslPolicyErrors, bool>)method
            .CreateDelegate(typeof(Func<X509Certificate2, X509Chain, SslPolicyErrors, bool>));
    }

    private static void ThrowIfErrors(ImmutableArray<Diagnostic> diagnostics, int startLine, KestrunHost host, string languageLabel)
    {
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length == 0)
        {
            return;
        }

        host.Logger.Error("{Lang} client certificate validation compilation completed with {Count} error(s).", languageLabel, errors.Length);

        var sb = new StringBuilder();
        _ = sb.AppendLine($"{languageLabel} client certificate validation compilation failed:");
        foreach (var error in errors)
        {
            var location = error.Location.IsInSource
                ? $" at line {error.Location.GetLineSpan().StartLinePosition.Line - startLine + 1}"
                : string.Empty;
            var msg = $"  Error [{error.Id}]: {error.GetMessage()}{location}";
            host.Logger.Error(msg);
            _ = sb.AppendLine(msg);
        }

        throw new CompilationErrorException(sb.ToString().TrimEnd(), diagnostics);
    }

    private static int GetStartLine(string source, string marker)
    {
        var idx = source.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0)
        {
            return 0;
        }

        var line = 0;
        for (var i = 0; i < idx; i++)
        {
            if (source[i] == '\n')
            {
                line++;
            }
        }

        return line;
    }

    private static string WrapCSharp(string code)
        => $$"""
using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

public static class ClientCertValidationScript
{
    public static bool Validate(X509Certificate2 certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
    {
        // ---- User code starts here ----
{{Indent(code, 8)}}
    }
}
""";

    private static string WrapVbNet(string code)
        => $$"""
Imports System
Imports System.Net.Security
Imports System.Security.Cryptography.X509Certificates

Public Module ClientCertValidationScript
    Public Function Validate(certificate As X509Certificate2, chain As X509Chain, sslPolicyErrors As SslPolicyErrors) As Boolean
        ' ---- User code starts here ----
{{Indent(code, 8)}}
    End Function
End Module
""";

    private static string Indent(string code, int spaces)
    {
        var pad = new string(' ', spaces);
        var lines = code.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal)
            .Split('\n');
        return string.Join("\n", lines.Select(l => pad + l));
    }
}
