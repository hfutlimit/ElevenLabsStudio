using Caliburn.Micro;

namespace ElevenLabsStudio.Core.MVVM;

/// <summary>
/// Thin alias over Caliburn.Micro's <see cref="Screen"/> so the rest of the
/// solution only references <c>ElevenLabsStudio.Core.MVVM</c> instead of
/// pulling the Caliburn namespace into every VM file. Keeping the alias
/// inside Core also means swapping MVVM frameworks only touches one file.
/// </summary>
public abstract class ScreenBase : Screen
{
    private bool _isBusy;
    private string? _busyMessage;

    public bool IsBusy
    {
        get => _isBusy;
        set => Set(ref _isBusy, value);
    }

    public string? BusyMessage
    {
        get => _busyMessage;
        set => Set(ref _busyMessage, value);
    }
}