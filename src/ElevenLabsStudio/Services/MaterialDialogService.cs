using System.Windows;
using ElevenLabsStudio.Core.Abstractions;

namespace ElevenLabsStudio.Services;

/// <summary>
/// WPF / <c>System.Windows.MessageBox</c> implementation of
/// <see cref="IDialogService"/>. Lives in the UI layer so Core can stay
/// UI-framework-free. Each call is <c>async</c> so VMs can await
/// confirmation without blocking the UI thread.
/// <para>
/// We deliberately do not bring MaterialDesign's <c>DialogHost</c>
/// into this service: the messages we surface are short (success/failure
/// of background operations) and the standard MessageBox is enough for
/// the MVP. A future "themed confirm dialog" can replace this without
/// changing the Core abstraction.
/// </para>
/// </summary>
public sealed class MaterialDialogService : IDialogService
{
    public Task ShowInfoAsync(string title, string message, CancellationToken ct = default)
    {
        Application.Current?.Dispatcher.Invoke(() =>
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information));
        return Task.CompletedTask;
    }

    public Task<bool> ConfirmAsync(string title, string message, CancellationToken ct = default)
    {
        var result = Application.Current?.Dispatcher.Invoke(() =>
            MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question));
        return Task.FromResult(result == MessageBoxResult.Yes);
    }

    public Task ShowErrorAsync(string title, string message, CancellationToken ct = default)
    {
        Application.Current?.Dispatcher.Invoke(() =>
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Three-way unsaved-changes prompt. Yes = keep the draft, No = throw
    /// the edits away, Cancel = abort whatever triggered this. Any failure
    /// to actually ask (no Application, no dispatcher) degrades to
    /// <see cref="UnsavedChangesDecision.Cancel"/> so we never silently
    /// discard work the user was never asked about.
    /// </summary>
    public Task<UnsavedChangesDecision> ResolveUnsavedChangesAsync(
        string title,
        string message,
        CancellationToken ct = default)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return Task.FromResult(UnsavedChangesDecision.Cancel);
        }

        var result = dispatcher.Invoke(() => MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning));

        return Task.FromResult(result switch
        {
            MessageBoxResult.Yes => UnsavedChangesDecision.SaveDraft,
            MessageBoxResult.No => UnsavedChangesDecision.Discard,
            _ => UnsavedChangesDecision.Cancel,
        });
    }
}