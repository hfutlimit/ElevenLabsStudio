namespace ElevenLabsStudio.Core.Abstractions;

/// <summary>
/// UI-neutral answer to "you have unsaved edits — what now?".
/// The WPF dialog maps Yes / No / Cancel onto these values and is the
/// only place that knows about <c>MessageBoxResult</c>.
/// </summary>
public enum UnsavedChangesDecision
{
	/// <summary>Keep the edits by snapshotting them into the draft store.</summary>
	SaveDraft,

	/// <summary>Throw the edits away and continue.</summary>
	Discard,

	/// <summary>Abort the navigation / close and keep editing.</summary>
	Cancel,
}
