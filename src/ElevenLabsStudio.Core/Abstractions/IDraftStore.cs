namespace ElevenLabsStudio.Core.Abstractions;

/// <summary>
/// Keyed in-memory scratchpad for per-agent edit drafts. The store
/// survives only for the lifetime of the process (or until the host
/// explicitly clears it) — drafts are never persisted to disk, so a
/// restart is the same as a deliberate "discard everything".
/// <para>
/// Used by the agent-detail screen to preserve in-progress edits
/// when the user navigates away from an agent in the sidebar. Picking
/// the same agent again (or a refresh) rehydrates the draft.
/// </para>
/// </summary>
public interface IDraftStore
{
    /// <summary>Persist <paramref name="value"/> under <paramref name="key"/>,
    /// overwriting any previous draft for the same key.</summary>
    void Save<T>(string key, T value);

    /// <summary>Read a previously-saved draft. Returns <c>false</c> when
    /// no draft has ever been saved under <paramref name="key"/>.</summary>
    bool TryGet<T>(string key, out T? value);

    /// <summary>True iff <see cref="Save{T}"/> has been called for the
    /// given key and the result has not been discarded.</summary>
    bool Has(string key);

    /// <summary>Forget the draft under <paramref name="key"/>. No-op
    /// when no draft exists.</summary>
    void Discard(string key);

    /// <summary>Enumerate every key currently held by the store. Used
    /// by the host to clean up drafts on shutdown or after a
    /// successful push.</summary>
    IReadOnlyCollection<string> Keys { get; }

    /// <summary>Remove every draft. Called on app exit so the next
    /// session starts clean.</summary>
    void ClearAll();
}
