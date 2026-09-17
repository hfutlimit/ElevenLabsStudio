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
}