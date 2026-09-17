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

    public bool MockMode { get; set; }

    public string MockBadgeText => MockMode ? "ON" : "OFF";

    public string StatusText { get; private set; } = string.Empty;

    public async Task SaveAsync()
    {
        var opts = _options.CurrentValue;
        var oldMock = opts.Mock;
        opts.ApiKey = ApiKey;
        opts.Mock = MockMode;

        try
        {
            await PersistToFileAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist settings to appsettings.json");
            StatusText = "保存失败：" + ex.Message;
            await _dialog.ShowErrorAsync(
                "保存失败",
                "无法写入 appsettings.json: " + ex.Message);
            return;
        }

        // Reload so dependent configuration (the IHttpClientFactory
        // base address, for example) re-resolves with the new values.
        _configRoot.Reload();

        _logger.LogInformation(
            "Settings persisted: mock={Mock} (was {OldMock}), apiKeySet={HasKey}",
            opts.Mock, oldMock, !string.IsNullOrWhiteSpace(opts.ApiKey));

        var restartNote = opts.Mock != oldMock
            ? "\n\nMock 模式已切换，需要重启应用让 IHttpClient/Mock 选择器重读配置。"
            : string.Empty;

        await _dialog.ShowInfoAsync(
            "Settings saved",
            $"配置已写入 appsettings.json 并热重载。{restartNote}",
            default);
        await TryCloseAsync(true);
    }

    public Task CancelAsync() => TryCloseAsync(false);

    private async Task PersistToFileAsync()
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
        elNode["Mock"] = JsonValue.Create(MockMode);
        elNode["ApiKey"] = JsonValue.Create(ApiKey);
        rootNode["ElevenLabs"] = elNode;

        var pretty = rootNode.ToJsonString(WriteOptions);
        await File.WriteAllTextAsync(path, pretty);
    }
}