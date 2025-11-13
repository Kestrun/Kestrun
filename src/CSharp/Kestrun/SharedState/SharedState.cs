using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Kestrun.SharedState;

/// <summary>
/// Thread‑safe, case‑insensitive global key/value store for reference‑type objects.
/// </summary>
public partial class SharedState
{
    // ── configuration ───────────────────────────────────────────────
    private static readonly Regex _validName =
        MyRegex();

    // StringComparer.OrdinalIgnoreCase ⇒ 100 % case‑insensitive keys
    private readonly ConcurrentDictionary<string, object?> _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="SharedState"/> class.
    /// </summary>
    /// <param name="ordinalIgnoreCase">If <c>true</c>, keys are compared in a case-insensitive manner; otherwise, case-sensitive.</param>
    public SharedState(bool ordinalIgnoreCase = true)
    {
        if (ordinalIgnoreCase)
        {
            _store = new(StringComparer.OrdinalIgnoreCase);
            return;
        }
        else
        {
            _store = new(StringComparer.Ordinal);
        }
    }

    // ── public API ──────────────────────────────────────────────────
    /// <summary>
    /// Add or overwrite a value (reference‑types only).
    /// </summary>
    /// <param name="name">The name of the variable to set.</param>
    /// <param name="value">The value to set. Must be a reference type unless <paramref name="allowsValueType"/> is <c>true</c>.</param>
    /// <param name="allowsValueType">If <c>true</c>, allows setting value types; otherwise, only reference types are allowed.</param>
    /// <returns><c>true</c> if the value was set successfully.</returns>
    public bool Set(string name, object? value, bool allowsValueType = false)
    {
        ValidateName(name);
        ValidateValue(name, value, allowsValueType);
        _store[name] = value;
        return true;
    }

    /// <summary>
    /// Checks if a variable with the specified name exists in the shared state.
    /// </summary>
    /// <param name="name">The name of the variable to check.</param>
    /// <returns> <c>true</c> if the variable exists; otherwise, <c>false</c>.</returns>
    public bool Contains(string name) => _store.ContainsKey(name);

    /// <summary>
    /// Strongly‑typed fetch. Returns <c>false</c> if the key is missing
    /// or the stored value can’t be cast to <typeparamref name="T"/>.
    /// </summary>
    public bool TryGet<T>(string name, out T? value)
    {
        if (_store.TryGetValue(name, out var raw) && raw is T cast)
        {
            value = cast;
            return true;
        }
        value = default;
        return false;
    }

    /// <summary>Untyped fetch; <c>null</c> if absent.</summary>
    public object? Get(string name) =>
        _store.TryGetValue(name, out var val) ? val : null;

    /// <summary>Snapshot of *all* current variables (shallow copy).</summary>
    public IReadOnlyDictionary<string, object?> Snapshot() =>
        _store.ToDictionary(kvp => kvp.Key, kvp => kvp.Value,
                            StringComparer.OrdinalIgnoreCase);

    /// <summary>Snapshot of keys only—handy for quick listings.</summary>
    public IReadOnlyCollection<string> KeySnapshot() =>
        [.. _store.Keys];

    /// <summary>
    /// Clears all entries in the shared state.
    /// </summary>
    public void Clear() =>
        _store.Clear();

    /// <summary>
    /// Gets the number of key/value pairs in the shared state.
    /// </summary>
    public int Count =>
        _store.Count;
    /// <summary>
    /// Gets an enumerable collection of all keys in the shared state.
    /// </summary>
    public IEnumerable<string> Keys =>
        _store.Keys;
    /// <summary>
    /// Gets an enumerable collection of all values in the shared state.
    /// </summary>
    public IEnumerable<object?> Values =>
        _store.Values;

    /// <summary>
    /// Attempts to remove the value with the specified name from the shared state.
    /// </summary>
    /// <param name="name">The name of the variable to remove.</param>
    /// <returns><c>true</c> if the variable was successfully removed; otherwise, <c>false</c>.</returns>
    public bool Remove(string name) =>
            _store.Remove(name, out _);

    // ── helpers ────────────────────────────────────────────────────
    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !_validName.IsMatch(name))
        {
            throw new ArgumentException(
                $"'{name}' is not a valid identifier for C# / PowerShell.",
                nameof(name));
        }
    }

    /// <summary>
    /// Validates the value to be stored in the shared state.
    /// </summary>
    /// <param name="name">The name of the variable.</param>
    /// <param name="value">The value to validate.</param>
    /// <param name="allowsValueType">Indicates whether value types are allowed.</param>
    /// <exception cref="ArgumentException">Thrown if the value is a value type and value types are not allowed.</exception>
    private static void ValidateValue(string name, object? value, bool allowsValueType = false)
    {
        if (value is null)
        {
            return;                       // null is allowed
        }

        var t = value.GetType();
        if (t.IsValueType && !allowsValueType)
        {
            throw new ArgumentException(
                $"Cannot define global variable '{name}' of value type '{t.FullName}'. " +
                "Only reference types are allowed.",
                nameof(value));
        }
    }

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex MyRegex();
}
