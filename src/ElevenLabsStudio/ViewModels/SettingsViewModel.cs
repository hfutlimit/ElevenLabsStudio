using Caliburn.Micro;
using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ElevenLabsStudio.ViewModels;

/// <summary>
/// Backs the Settings dialog. Reads / writes
/// <see cref="ElevenLabsOptions"/> via <see cref="IOptionsMonitor{T}"/>
/// so the form is pre-populated from appsettings.json / env vars, and
/// any Save click writes back to the underlying options instance via
/// <see cref="IOptionsMonitor{T}.CurrentValue"/> mutations.
/// </summary>
public sealed class SettingsViewModel : Screen
{
    private readonly IOptionsMonitor<ElevenLabsOptions> _options;
    private readonly IDialogService _dialog;
    private readonly ILogger<SettingsViewModel> _logger;

    public SettingsViewModel(
        IOptionsMonitor<ElevenLabsOptions> options,
        IDialogService dialog,
        ILogger<SettingsViewModel> logger)
    {
        _options = options;
        _dialog = dialog;
        _logger = logger;

        var opts = _options.CurrentValue;
        ApiKey = opts.ApiKey;
        MockMode = opts.Mock;
    }

    public string ApiKey { get; set; } = string.Empty;

    public bool MockMode { get; set; }

    public string? StatusText { get; set; }

    public string MockBadgeText => MockMode ? "ON" : "OFF";

    public async Task SaveAsync()
    {
        var opts = _options.CurrentValue;
        opts.ApiKey = ApiKey;
        opts.Mock = MockMode;
        _logger.LogInformation(
            "Settings updated: mock={Mock}, apiKeySet={HasKey}",
            opts.Mock, !string.IsNullOrWhiteSpace(opts.ApiKey));
        await _dialog.ShowInfoAsync(
            "Settings saved",
            "Changes are in-memory. Restart the app to revert to appsettings.json.",
            default);
        await TryCloseAsync(true);
    }

    public Task CancelAsync() => TryCloseAsync(false);
}