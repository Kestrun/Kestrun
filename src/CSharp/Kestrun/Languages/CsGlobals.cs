using Kestrun.Models;

namespace Kestrun.Languages;
// ---------------------------------------------------------------------------
//  C# delegate builder  –  now takes optional imports / references
// ---------------------------------------------------------------------------
/// <summary>
/// Provides global and local variable dictionaries and context for C# delegate execution.
/// </summary>
public record CsGlobals
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CsGlobals"/> class with the specified global variables.
    /// </summary>
    /// <param name="globals">A dictionary containing global variables.</param>
    public CsGlobals(IReadOnlyDictionary<string, object?> globals)
    {
        Globals = globals;
        Locals = new Dictionary<string, object?>();
        Context = null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CsGlobals"/> class with the specified global variables and context.
    /// </summary>
    /// <param name="globals">A dictionary containing global variables.</param>
    /// <param name="krcontext">The Kestrun execution context.</param>
    public CsGlobals(IReadOnlyDictionary<string, object?> globals, KestrunContext krcontext)
    {
        Globals = globals;
        Context = krcontext;
        Locals = new Dictionary<string, object?>();
    }

    /// <summary>
    /// Initializes a new instance with the specified global and local variables (no execution context).
    /// </summary>
    /// <param name="globals">Global variables.</param>
    /// <param name="locals">Local variables.</param>
    public CsGlobals(IReadOnlyDictionary<string, object?> globals, IReadOnlyDictionary<string, object?> locals)
    {
        Globals = globals;
        Locals = locals;
        Context = null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CsGlobals"/> class with the specified global variables, context, and local variables.
    /// </summary>
    /// <param name="globals">A dictionary containing global variables.</param>
    /// <param name="krcontext">The Kestrun execution context.</param>
    /// <param name="locals">A dictionary containing local variables.</param>
    public CsGlobals(IReadOnlyDictionary<string, object?> globals, KestrunContext krcontext, IReadOnlyDictionary<string, object?> locals)
    {
        Globals = globals;
        Context = krcontext;
        Locals = locals;
    }

    /// <summary>
    /// Gets the dictionary containing global variables for delegate execution.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Globals { get; }

    /// <summary>
    /// Gets the dictionary containing local variables for delegate execution.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Locals { get; }

    /// <summary>
    /// Gets the Kestrun execution context for delegate execution.
    /// </summary>
    public KestrunContext? Context { get; }
}
