using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authentication;

namespace Kestrun.Authentication;

/// <summary>
/// Registry of authentication options keyed by (schema, type).
/// Stores as base AuthenticationSchemeOptions, with typed helpers.
/// </summary>
public sealed class AuthenticationRegistry
{
    private readonly ConcurrentDictionary<(string schema, string type), AuthenticationSchemeOptions> _map;
    private readonly StringComparer _stringComparer;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticationRegistry"/> class.
    /// </summary>
    /// <param name="comparer">The string comparer to use for matching schemas and types.</param>
    public AuthenticationRegistry(StringComparer? comparer = null)
    {
        _stringComparer = comparer ?? StringComparer.Ordinal;
        _map = new ConcurrentDictionary<(string, string), AuthenticationSchemeOptions>(new TupleComparer(_stringComparer));
    }

    // ---------- Register / Upsert ----------

    /// <summary>
    /// Registers an authentication scheme with the specified options.
    /// </summary>
    /// <param name="schema">The schema to match for the authentication scheme.</param>
    /// <param name="type">The HTTP type to match for the authentication scheme.</param>
    /// <param name="options">The options to configure the authentication scheme.</param>
    /// <returns>True if the registration was successful; otherwise, false.</returns>
    public bool Register(string schema, string type, AuthenticationSchemeOptions options)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(options);
        return _map.TryAdd((schema, type), options);
    }

    /// <summary>
    /// Registers an authentication scheme with the specified options and configuration.
    /// </summary>
    /// <typeparam name="TOptions">The type of the options for the authentication scheme.</typeparam>
    /// <param name="schema">The schema to match for the authentication scheme.</param>
    /// <param name="type">The HTTP type to match for the authentication scheme.</param>
    /// <param name="configure">An optional action to configure the authentication options.</param>
    /// <returns>True if the registration was successful; otherwise, false.</returns>
    public bool Register<TOptions>(string schema, string type, Action<TOptions>? configure = null)
        where TOptions : AuthenticationSchemeOptions, new()
    {
        var opts = new TOptions();
        configure?.Invoke(opts);
        return Register(schema, type, opts);
    }

    /// <summary>
    /// Upserts (adds or replaces) an entry.
    /// </summary>
    /// <param name="schema">The schema to match for the authentication scheme.</param>
    /// <param name="type">The HTTP type to match for the authentication scheme.</param>
    /// <param name="options">The options to configure the authentication scheme.</param>
    /// <exception cref="ArgumentNullException">Thrown when any of the arguments are null.</exception>
    public void Upsert(string schema, string type, AuthenticationSchemeOptions options)
    {
        _map[(schema ?? throw new ArgumentNullException(nameof(schema)),
             type ?? throw new ArgumentNullException(nameof(type)))] = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Upserts (adds or replaces) an entry.
    /// </summary>
    /// <typeparam name="TOptions">The type of the options for the authentication scheme.</typeparam>
    /// <param name="schema">The schema to match for the authentication scheme.</param>
    /// <param name="type">The HTTP type to match for the authentication scheme.</param>
    /// <param name="configure">An optional action to configure the authentication options.</param>
    public void Upsert<TOptions>(string schema, string type, Action<TOptions>? configure = null)
        where TOptions : AuthenticationSchemeOptions, new()
    {
        var opts = new TOptions();
        configure?.Invoke(opts);
        Upsert(schema, type, opts);
    }

    // ---------- Exists / TryGet / Get ----------

    /// <summary>
    /// Checks if an authentication scheme exists for the specified schema and type.
    /// </summary>
    /// <param name="schema">The schema to match for the authentication scheme.</param>
    /// <param name="type">The HTTP type to match for the authentication scheme.</param>
    /// <returns>True if an authentication scheme exists; otherwise, false.</returns>
    public bool Exists(string schema, string type)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(type);
        return _map.ContainsKey((schema, type));
    }

    /// <summary>
    /// Tries to get the authentication options for the specified schema and type.
    /// </summary>
    /// <param name="schema">The schema to match for the authentication scheme.</param>
    /// <param name="type">The HTTP type to match for the authentication scheme.</param>
    /// <param name="options">The options for the authentication scheme.</param>
    /// <returns>True if the authentication options were found; otherwise, false.</returns>
    public bool TryGet(string schema, string type, out AuthenticationSchemeOptions? options)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(type);
        return _map.TryGetValue((schema, type), out options);
    }

    /// <summary>
    /// Tries to get the authentication options of the specified type for the given schema and type.
    /// </summary>
    /// <typeparam name="TOptions">The type of the authentication options.</typeparam>
    /// <param name="schema">The schema to match for the authentication scheme.</param>
    /// <param name="type">The HTTP type to match for the authentication scheme.</param>
    /// <param name="options">The options for the authentication scheme.</param>
    /// <returns>True if the authentication options were found; otherwise, false.</returns>
    public bool TryGet<TOptions>(string schema, string type, out TOptions? options)
        where TOptions : AuthenticationSchemeOptions
    {
        if (_map.TryGetValue((schema, type), out var baseOpts) && baseOpts is TOptions typed)
        {
            options = typed;
            return true;
        }
        options = null;
        return false;
    }

    /// <summary>
    /// Gets the authentication options for the specified schema and type.
    /// </summary>
    /// <param name="schema">The schema to match for the authentication scheme.</param>
    /// <param name="type">The HTTP type to match for the authentication scheme.</param>
    /// <returns>The authentication options for the specified schema and type.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no authentication options are registered for the specified schema and type.</exception>
    public AuthenticationSchemeOptions Get(string schema, string type)
    {
        return !TryGet(schema, type, out var opts)
            ? throw new KeyNotFoundException($"No authentication registered for schema='{schema}', type='{type}'.")
            : opts!;
    }

    /// <summary>
    /// Gets the authentication options of the specified type for the given schema and type.
    /// </summary>
    /// <typeparam name="TOptions">The type of the authentication options.</typeparam>
    /// <param name="schema">The schema to match for the authentication scheme.</param>
    /// <param name="type">The HTTP type to match for the authentication scheme.</param>
    /// <returns>The authentication options of the specified type for the given schema and type.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no authentication options of the specified type are registered for the given schema and type.</exception>
    public TOptions Get<TOptions>(string schema, string type)
        where TOptions : AuthenticationSchemeOptions
    {
        return !TryGet<TOptions>(schema, type, out var opts)
            ? throw new KeyNotFoundException($"No authentication of type {typeof(TOptions).Name} for schema='{schema}', type='{type}'.")
            : opts!;
    }

    // ---------- Remove / Clear / Enumerate ----------

    /// <summary>
    /// Removes the authentication scheme for the specified schema and type.
    /// </summary>
    /// <param name="schema">The schema to match for the authentication scheme.</param>
    /// <param name="type">The HTTP type to match for the authentication scheme.</param>
    /// <returns>True if the authentication scheme was removed; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either schema or type is null.</exception>
    public bool Remove(string schema, string type)
        => _map.TryRemove((schema ?? throw new ArgumentNullException(nameof(schema)),
                           type ?? throw new ArgumentNullException(nameof(type))), out _);

    /// <summary>
    /// Clears all registered authentication schemes.
    /// </summary>
    public void Clear() => _map.Clear();

    /// <summary>
    /// Enumerates all registered authentication schemes.
    /// </summary>
    /// <returns>A collection of key-value pairs representing the registered authentication schemes.</returns>
    public IEnumerable<KeyValuePair<(string schema, string type), AuthenticationSchemeOptions>> Items()
        => _map;

    // ---------- Internal tuple comparer (case-insensitive support) ----------

    private sealed class TupleComparer(StringComparer cmp) : IEqualityComparer<(string schema, string type)>
    {
        private readonly StringComparer _cmp = cmp;

        public bool Equals((string schema, string type) x, (string schema, string type) y)
            => _cmp.Equals(x.schema, y.schema) && _cmp.Equals(x.type, y.type);
        public int GetHashCode((string schema, string type) obj)
            => HashCode.Combine(_cmp.GetHashCode(obj.schema), _cmp.GetHashCode(obj.type));
    }
}
