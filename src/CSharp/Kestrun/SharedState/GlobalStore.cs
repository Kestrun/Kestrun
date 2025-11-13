namespace Kestrun.SharedState;

/// <summary>
/// Thread‑safe, case‑insensitive global key/value store for reference‑type objects.
/// </summary>
public static class GlobalStore
{
    private static readonly SharedState _store = new();

    // ── public API ──────────────────────────────────────────────────
    /// <summary>Add or overwrite a value (reference‑types only).</summary>
    public static bool Set(string name, object? value, bool allowsValueType = false) =>
        _store.Set(name, value, allowsValueType);

    /// <summary>
    /// Checks if a variable with the specified name exists in the shared state.
    /// </summary>
    /// <param name="name">The name of the variable to check.</param>
    /// <returns> <c>true</c> if the variable exists; otherwise, <c>false</c>.</returns>
    public static bool Contains(string name) => _store.Contains(name);

    /// <summary>
    /// Strongly‑typed fetch. Returns <c>false</c> if the key is missing
    /// or the stored value can’t be cast to <typeparamref name="T"/>.
    /// </summary>
    public static bool TryGet<T>(string name, out T? value) =>
        _store.TryGet(name, out value);

    /// <summary>Untyped fetch; <c>null</c> if absent.</summary>
    public static object? Get(string name) =>
        _store.Get(name);
    /// <summary>Snapshot of *all* current variables (shallow copy).</summary>
    public static IReadOnlyDictionary<string, object?> Snapshot() =>
        _store.Snapshot();

    /// <summary>Snapshot of keys only—handy for quick listings.</summary>
    public static IReadOnlyCollection<string> KeySnapshot() =>
        _store.KeySnapshot();
    /// <summary>
    /// Clears all entries in the shared state.
    /// </summary>
    public static void Clear() =>
        _store.Clear();

    /// <summary>
    /// Gets the number of key/value pairs in the shared state.
    /// </summary>
    public static int Count =>
        _store.Count;
    /// <summary>
    /// Gets an enumerable collection of all keys in the shared state.
    /// </summary>
    public static IEnumerable<string> Keys =>
        _store.Keys;
    /// <summary>
    /// Gets an enumerable collection of all values in the shared state.
    /// </summary>
    public static IEnumerable<object?> Values =>
        _store.Values;

    /// <summary>
    /// Attempts to remove the value with the specified name from the shared state.
    /// </summary>
    /// <param name="name">The name of the variable to remove.</param>
    /// <returns><c>true</c> if the variable was successfully removed; otherwise, <c>false</c>.</returns>
    public static bool Remove(string name) =>
            _store.Remove(name);
}
