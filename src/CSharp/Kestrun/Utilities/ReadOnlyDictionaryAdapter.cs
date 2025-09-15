using System.Collections;

namespace Kestrun.Utilities;

/// <summary>
/// Adapts a non-generic IDictionary to a read-only dictionary with string keys and nullable object values.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ReadOnlyDictionaryAdapter"/> class with the specified non-generic dictionary.
/// </remarks>
/// <param name="inner">The non-generic <see cref="IDictionary"/> to adapt.</param>
/// <exception cref="ArgumentNullException">Thrown when <paramref name="inner"/> is null.</exception>
public sealed class ReadOnlyDictionaryAdapter(IDictionary inner) : IReadOnlyDictionary<string, object?>
{
    private readonly IDictionary _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    /// <summary>
    /// Gets the value associated with the specified string key, or null if the key does not exist.
    /// </summary>
    /// <param name="key">The string key whose value to get.</param>
    /// <returns>The value associated with the specified key, or null if the key is not found.</returns>
    public object? this[string key]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(key);
            return _inner.Contains(key) ? _inner[key] : null;
        }
    }

    /// <summary>
    /// Gets an enumerable collection of the string keys in the adapted dictionary.
    /// </summary>
    public IEnumerable<string> Keys =>
        _inner.Keys.Cast<object>().Select(k => k?.ToString() ?? string.Empty);

    /// <summary>
    /// Gets an enumerable collection of the values in the adapted dictionary.
    /// </summary>
    public IEnumerable<object?> Values =>
        Keys.Select(k => this[k]);

    /// <summary>
    /// Gets the number of key/value pairs contained in the adapted dictionary.
    /// </summary>
    public int Count => _inner.Count;

    /// <summary>
    /// Determines whether the adapted dictionary contains an element with the specified string key.
    /// </summary>
    /// <param name="key">The string key to locate in the dictionary.</param>
    /// <returns>true if the dictionary contains an element with the specified key; otherwise, false.</returns>
    public bool ContainsKey(string key) => key is not null && _inner.Contains(key);

    /// <summary>
    /// Attempts to get the value associated with the specified string key.
    /// </summary>
    /// <param name="key">The string key whose value to get.</param>
    /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, null.</param>
    /// <returns>true if the dictionary contains an element with the specified key; otherwise, false.</returns>
    public bool TryGetValue(string key, out object? value)
    {
        if (key is null)
        {
            value = null;
            return false;
        }

        if (_inner.Contains(key))
        {
            value = _inner[key];
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Returns an enumerator that iterates through the key/value pairs in the adapted dictionary.
    /// </summary>
    /// <returns>An enumerator for the key/value pairs.</returns>
    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        foreach (DictionaryEntry entry in _inner)
        {
            var k = entry.Key?.ToString()
                       ?? throw new InvalidOperationException("Underlying dictionary contains a null key.");
            yield return new KeyValuePair<string, object?>(k, entry.Value);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
