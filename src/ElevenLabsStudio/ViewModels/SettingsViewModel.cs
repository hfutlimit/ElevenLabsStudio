using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Caliburn.Micro;
using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using JsonValue = System.Text.Json.Nodes.JsonValue;

namespace ElevenLabsStudio.ViewModels;

/// <summary>
/// Backs the Settings dialog. Reads from
/// <see cref="IOptionsMonitor{T}"/> so the form is pre-populated from
/// appsettings.json + env vars, and any Save click writes the
/// ElevenLabs section back to appsettings.json on disk and reloads
/// the IConfiguration so the new values take effect immediately.
/// </summary>
public sealed class SettingsViewModel : Screen
{
    private readonly IOptionsMonitor<ElevenLabsOptions> _options;
    private readonly IDialogService _dialog;
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly IConfigurationRoot _configRoot;
    private readonly string? _appsettingsPathOverride;

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
    };

    public SettingsViewModel(
        IOptionsMonitor<ElevenLabsOptions> options,
        IDialogService dialog,
        ILogger<SettingsViewModel> logger,
        IConfiguration configuration,
        string? appsettingsPathOverride = null)
    {
        _options = options;
        _dialog = dialog;
        _logger = logger;
        _appsettingsPathOverride = appsettingsPathOverride;
        // IConfigurationRoot is what gives us the ability to Reload()
        // after writing. When the host built the configuration, this
        // was the actual root it created.
        _configRoot = configuration as IConfigurationRoot
            ?? throw new InvalidOperationException(
                "Configuration is not an IConfigurationRoot; cannot persist settings.");

        var opts = _options.CurrentValue;
        ApiKey = opts.ApiKey;
        MockMode = opts.Mock;
    }

    public string ApiKey { get; set; } = string.Empty;

    private bool _mockMode;

    /// <summary>
    /// Two-way bound to the dialog checkbox. Raises change
    /// notifications so the ON/OFF <see cref="MockBadgeText"/> badge
    /// re-renders the moment the user flips the toggle instead of
    /// staying stuck on the initial value (review #8).
    /// </summary>
    public bool MockMode
    {
        get => _mockMode;
        set
        {
            if (Set(ref _mockMode, value))
            {
                NotifyOfPropertyChange(nameof(MockBadgeText));
            }
        }
    }

    public string MockBadgeText => MockMode ? "ON" : "OFF";

    public string StatusText { get; private set; } = string.Empty;

    public async Task SaveAsync()
    {
        var opts = _options.CurrentValue;
        var oldMock = opts.Mock;

        // Capture the intended new values up front so the file write
        // can happen without first mutating the live in-memory options.
        // If the write fails the live options are untouched and the
        // user sees an honest "Save failed" message instead of a UI
        // showing "saved" while the running app still uses the old
        // Mock/ApiKey (review #5).
        var snapshot = new ElevenLabsOptions
        {
            Mock = MockMode,
            ApiKey = ApiKey,
            BaseUrl = opts.BaseUrl,
        };

        try
        {
            await PersistToFileAsync(snapshot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist settings to appsettings.json");
            StatusText = "Save failed: " + ex.Message;
            await _dialog.ShowErrorAsync(
                "Save failed",
                "Could not write appsettings.json: " + ex.Message);
            return;
        }

        // Persist succeeded — now and only now do we touch the
        // in-memory IOptionsMonitor surface and trigger Reload so
        // every consumer (RuntimeClient, typed HttpClient, MS logger
        // sinks) re-resolves with the new values.
        opts.Mock = MockMode;
        opts.ApiKey = ApiKey;
        _configRoot.Reload();

        _logger.LogInformation(
            "Settings persisted: mock={Mock} (was {OldMock}), apiKeySet={HasKey}",
            opts.Mock, oldMock, !string.IsNullOrWhiteSpace(opts.ApiKey));

        var note = opts.Mock != oldMock
            ? "\n\nMock mode change applied — the next agent list load will use the new data source; no restart required."
            : string.Empty;

        await _dialog.ShowInfoAsync(
            "Saved",
            $"Configuration written to appsettings.json and hot-reloaded.{note}",
            default);
        await TryCloseAsync(true);
    }

    public Task CancelAsync() => TryCloseAsync(false);

    private async Task PersistToFileAsync(ElevenLabsOptions snapshot)
    {
        var path = _appsettingsPathOverride
            ?? Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("appsettings.json not found next to the .exe.", path);
        }

        // Read existing JSON tree, replace ElevenLabs section values
        // we care about, keep every other section byte-for-byte.
        var raw = await File.ReadAllTextAsync(path);
        var rootNode = JsonNode.Parse(raw, documentOptions: new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        }) as JsonObject
            ?? throw new InvalidOperationException("appsettings.json root is not a JSON object.");

        var elNode = rootNode["ElevenLabs"] as JsonObject ?? new JsonObject();
        elNode["Mock"] = JsonValue.Create(snapshot.Mock);
        elNode["ApiKey"] = JsonValue.Create(snapshot.ApiKey);
        rootNode["ElevenLabs"] = elNode;

        var pretty = rootNode.ToJsonString(WriteOptions);
        await File.WriteAllTextAsync(path, pretty);
    }
}
