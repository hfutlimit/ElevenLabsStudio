using System.Collections.Concurrent;
using ElevenLabsStudio.Core.Abstractions;

namespace ElevenLabsStudio.Infrastructure.Memory;

/// <summary>
/// Process-local <see cref="IDraftStore"/> backed by a
/// <see cref="ConcurrentDictionary{TKey, TValue}"/>. Drafts are lost
/// when the process exits; callers that need a "forget on success"
/// policy should call <see cref="Discard"/> after the change is
/// committed (e.g. the right side of an <c>AgentDetailViewModel.Push</c>).
/// </summary>
public sealed class MemoryDraftStore : IDraftStore
{
    private readonly ConcurrentDictionary<string, object> _store = new();

    public void Save<T>(string key, T value)
    {
        ArgumentNullException.ThrowIfNull(key);
        _store[key] = value!;
    }

    public bool TryGet<T>(string key, out T? value)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (_store.TryGetValue(key, out var raw) && raw is T typed)
        {
            value = typed;
            return true;
        }
        value = default;
        return false;
    }

    public bool Has(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _store.ContainsKey(key);
    }

    public void Discard(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        _store.TryRemove(key, out _);
    }

    public IReadOnlyCollection<string> Keys => _store.Keys.ToArray();

    public void ClearAll() => _store.Clear();
}
