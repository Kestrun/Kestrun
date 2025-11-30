using System.Collections.Concurrent;
using Kestrun.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Kestrun.Authentication;

/// <summary>
/// Registry of authentication options keyed by (schema, type).
/// Stores as base AuthenticationSchemeOptions, with typed helpers.
/// </summary>
public sealed class AuthenticationRegistry
{
    private readonly ConcurrentDictionary<(string schema, AuthenticationType type), AuthenticationSchemeOptions> _map;
    private readonly StringComparer _stringComparer;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticationRegistry"/> class.
    /// </summary>
    /// <param name="comparer">The string comparer to use for matching schemas and types.</param>
    public AuthenticationRegistry(StringComparer? comparer = null)
    {
        _stringComparer = comparer ?? StringComparer.Ordinal;
        _map = new ConcurrentDictionary<(string, AuthenticationType), AuthenticationSchemeOptions>(new TupleComparer(_stringComparer));
    }

    // ---------- Register / Upsert ----------

    /// <summary>
    /// Registers an authentication scheme with the specified options.
    /// </summary>
    /// <param name="schema">The schema to match for the authentication scheme.</param>
    /// <param name="type">The HTTP type to match for the authentication scheme.</param>
    /// <param name="options">The options to configure the authentication scheme.</param>
    /// <returns>True if the registration was successful; otherwise, false.</returns>
    public bool Register(string schema, AuthenticationType type, AuthenticationSchemeOptions options)
    {
        ArgumentNullException.ThrowIfNull(schema);
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
    public bool Register<TOptions>(string schema, AuthenticationType type, Action<TOptions>? configure = null)
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
    public void Upsert(string schema, AuthenticationType type, AuthenticationSchemeOptions options)
    {
        _map[(schema ?? throw new ArgumentNullException(nameof(schema)), type)] =
            options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Upserts (adds or replaces) an entry.
    /// </summary>
    /// <typeparam name="TOptions">The type of the options for the authentication scheme.</typeparam>
    /// <param name="schema">The schema to match for the authentication scheme.</param>
    /// <param name="type">The HTTP type to match for the authentication scheme.</param>
    /// <param name="configure">An optional action to configure the authentication options.</param>
    public void Upsert<TOptions>(string schema, AuthenticationType type, Action<TOptions>? configure = null)
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
    public bool Exists(string schema, AuthenticationType type)
    {
        ArgumentNullException.ThrowIfNull(schema);
        return _map.ContainsKey((schema, type));
    }

    /// <summary>
    /// Tries to get the authentication options for the specified schema and type.
    /// </summary>
    /// <param name="schema">The schema to match for the authentication scheme.</param>
    /// <param name="type">The HTTP type to match for the authentication scheme.</param>
    /// <param name="options">The options for the authentication scheme.</param>
    /// <returns>True if the authentication options were found; otherwise, false.</returns>
    public bool TryGet(string schema, AuthenticationType type, out AuthenticationSchemeOptions? options)
    {
        ArgumentNullException.ThrowIfNull(schema);
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
    public bool TryGet<TOptions>(string schema, AuthenticationType type, out TOptions? options)
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
    public AuthenticationSchemeOptions Get(string schema, AuthenticationType type)
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
    public TOptions Get<TOptions>(string schema, AuthenticationType type)
        where TOptions : AuthenticationSchemeOptions
    {
        return !TryGet<TOptions>(schema, type, out var opts)
            ? throw new KeyNotFoundException($"No authentication of type {typeof(TOptions).Name} for schema='{schema}', type='{type}'.")
            : opts!;
    }

    /// <summary>
    /// Gets the authentication scheme name for the specified schema and type.
    /// </summary>
    /// <param name="schema">The schema to match for the authentication scheme.</param>
    /// <param name="type">The HTTP type to match for the authentication scheme.</param>
    /// <param name="CookiesSchemeName">If true, returns the cookie scheme name for cookie-based options.</param>
    /// <returns>The authentication scheme name.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no authentication options are registered for the specified schema and type.</exception>
    public string ResolveAuthenticationSchemeName(string schema, AuthenticationType type, bool CookiesSchemeName)
    {
        if (!TryGet(schema, type, out var options))
        {
            throw new KeyNotFoundException($"No authentication registered for schema='{schema}', type='{type}'.");
        }
        // determine scheme name based on options type
        return options switch
        {
            OAuth2Options oauth2Opts => CookiesSchemeName ? oauth2Opts.CookieScheme : oauth2Opts.AuthenticationScheme,
            OidcOptions oidcOpts => CookiesSchemeName ? oidcOpts.CookieScheme : oidcOpts.AuthenticationScheme,
            BasicAuthenticationOptions => schema,
            JwtBearerOptions => schema,
            CookieAuthenticationOptions => schema,
            ApiKeyAuthenticationOptions => schema,
            // DigestAuthenticationOptions digestOpts => digestOpts.AuthenticationScheme,
            _ => schema
        };
    }

    // ---------- Remove / Clear / Enumerate ----------

    /// <summary>
    /// Removes the authentication scheme for the specified schema and type.
    /// </summary>
    /// <param name="schema">The schema to match for the authentication scheme.</param>
    /// <param name="type">The HTTP type to match for the authentication scheme.</param>
    /// <returns>True if the authentication scheme was removed; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either schema or type is null.</exception>
    public bool Remove(string schema, AuthenticationType type)
        => _map.TryRemove((schema ?? throw new ArgumentNullException(nameof(schema)), type), out _);

    /// <summary>
    /// Clears all registered authentication schemes.
    /// </summary>
    public void Clear() => _map.Clear();

    /// <summary>
    /// Enumerates all registered authentication schemes.
    /// </summary>
    /// <returns>A collection of key-value pairs representing the registered authentication schemes.</returns>
    public IEnumerable<KeyValuePair<(string schema, AuthenticationType type), AuthenticationSchemeOptions>> Items()
        => _map;

    /// <summary>
    /// Returns a dictionary of all registered OpenID Connect (OIDC) authentication options
    /// keyed by their schema. Only entries whose <see cref="AuthenticationType"/> is <see cref="AuthenticationType.Oidc"/>
    /// and whose stored options are of type <see cref="OidcOptions"/> are included.
    /// </summary>
    /// <returns>A dictionary mapping schema names to their <see cref="OidcOptions"/>.</returns>
    public IDictionary<string, OidcOptions> GetOidcOptions()
    {
        var result = new Dictionary<string, OidcOptions>(_stringComparer);
        foreach (var kvp in _map)
        {
            if (kvp.Key.type == AuthenticationType.Oidc && kvp.Value is OidcOptions oidc)
            {
                result[kvp.Key.schema] = oidc;
            }
        }
        return result;
    }

    /// <summary>
    /// Returns a dictionary mapping authentication schema names to the array of claim policy names
    /// defined for that schema. It inspects registered option objects for a property named
    /// <c>ClaimPolicyConfig</c> or <c>ClaimPolicy</c> of type <see cref="ClaimPolicyConfig"/>.
    /// Only schemas with at least one defined policy are returned.
    /// Supports <see cref="OidcOptions"/>, <see cref="OAuth2Options"/>, and other options types.
    /// </summary>
    /// <returns>A dictionary of schema -> string[] (policy names).</returns>
    public IDictionary<string, string[]> GetClaimPolicyConfigs()
    {
        var result = new Dictionary<string, string[]>(_stringComparer);
        foreach (var kvp in _map)
        {
            var options = kvp.Value;
            ClaimPolicyConfig? cfg = null;

            // Try explicit properties first (avoids reflection if we add strong-typed support later)
            if (options is OidcOptions oidc && oidc.ClaimPolicy is { } oidcCfg)
            {
                cfg = oidcCfg;
            }
            else if (options is OAuth2Options oauth2)
            {
                // OAuth2Options uses reflection fallback (no ClaimPolicy property currently)
                var prop = oauth2.GetType().GetProperty("ClaimPolicyConfig") ?? oauth2.GetType().GetProperty("ClaimPolicy");
                if (prop != null && typeof(ClaimPolicyConfig).IsAssignableFrom(prop.PropertyType))
                {
                    cfg = (ClaimPolicyConfig?)prop.GetValue(oauth2);
                }
            }

            if (cfg?.Policies is { Count: > 0 })
            {
                result[kvp.Key.schema] = [.. cfg.Policies.Keys];
            }
        }
        return result;
    }

    /// <summary>
    /// Returns a list of authentication schema names that define the specified policy name.
    /// This method searches through all registered authentication options and returns the schemas
    /// that have a <see cref="ClaimPolicyConfig"/> containing a policy with the given name.
    /// </summary>
    /// <param name="policyName">The name of the policy to search for.</param>
    /// <returns>A list of schema names that own the specified policy.</returns>
    public IList<string> GetSchemesByPolicy(string policyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);

        var result = new List<string>();
        foreach (var kvp in _map)
        {
            var options = kvp.Value;
            ClaimPolicyConfig? cfg = null;

            // Try explicit properties first
            if (options is OidcOptions oidc && oidc.ClaimPolicy is { } oidcCfg)
            {
                cfg = oidcCfg;
            }
            else if (options is OAuth2Options oauth2 && oauth2.ClaimPolicy is { } oauth2Cfg)
            {
                cfg = oauth2Cfg;
            }
            else
            {
                // Fallback: reflection search for ClaimPolicyConfig or ClaimPolicy
                var prop = options.GetType().GetProperty("ClaimPolicyConfig") ?? options.GetType().GetProperty("ClaimPolicy");
                if (prop != null && typeof(ClaimPolicyConfig).IsAssignableFrom(prop.PropertyType))
                {
                    cfg = (ClaimPolicyConfig?)prop.GetValue(options);
                }
            }

            if (cfg?.Policies != null && cfg.Policies.ContainsKey(policyName))
            {
                result.Add(kvp.Key.schema);
            }
        }
        return result;
    }

    // ---------- Internal tuple comparer (case-insensitive support) ----------

    private sealed class TupleComparer(StringComparer cmp) : IEqualityComparer<(string schema, AuthenticationType type)>
    {
        private readonly StringComparer _cmp = cmp;

        public bool Equals((string schema, AuthenticationType type) x, (string schema, AuthenticationType type) y)
            => _cmp.Equals(x.schema, y.schema) && _cmp.Equals(x.type, y.type);
        public int GetHashCode((string schema, AuthenticationType type) obj)
            => HashCode.Combine(_cmp.GetHashCode(obj.schema), _cmp.GetHashCode(obj.type));
    }
}
