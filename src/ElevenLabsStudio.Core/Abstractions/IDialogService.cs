namespace ElevenLabsStudio.Core.Abstractions;

/// <summary>
/// Abstraction over any modal popup (info / confirm / error). UI projects
/// provide the real implementation; Core can therefore stay free of
/// <c>System.Windows</c> while still letting ViewModels surface
/// user-facing messages.
/// </summary>
public interface IDialogService
{
	Task ShowInfoAsync(string title, string message, CancellationToken ct = default);
	Task<bool> ConfirmAsync(string title, string message, CancellationToken ct = default);
	Task ShowErrorAsync(string title, string message, CancellationToken ct = default);

	/// <summary>
	/// Ask the user what to do with unsaved edits. Implementations must
	/// return <see cref="UnsavedChangesDecision.Cancel"/> when no dialog
	/// could be shown (missing dispatcher, unexpected result) so callers
	/// never destroy work they failed to ask about.
	/// </summary>
	Task<UnsavedChangesDecision> ResolveUnsavedChangesAsync(
		string title,
		string message,
		CancellationToken ct = default);
}
