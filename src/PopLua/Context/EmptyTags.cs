using System.Collections;

namespace PopLua.Context;

internal sealed class EmptyTags : IReadOnlyDictionary<string, object>
{
    public static EmptyTags Instance { get; } = new();

    private EmptyTags()
    {
    }

    public int Count => 0;
    public IEnumerable<string> Keys => [];
    public IEnumerable<object> Values => [];
    public object this[string key] => throw new KeyNotFoundException(key);

    public bool ContainsKey(string key) => false;

    public bool TryGetValue(string key, out object value)
    {
        value = null!;
        return false;
    }

    public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        => Enumerable.Empty<KeyValuePair<string, object>>().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
