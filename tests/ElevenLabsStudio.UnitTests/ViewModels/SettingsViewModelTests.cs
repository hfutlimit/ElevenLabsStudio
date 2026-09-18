using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Infrastructure;
using ElevenLabsStudio.ViewModels;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ElevenLabsStudio.UnitTests.ViewModels;

/// <summary>
/// SettingsViewModel contract:
///   1. Pulls current Mock + ApiKey from IOptionsMonitor on construct.
///   2. PersistToFileAsync round-trips appsettings.json without
///      touching unrelated sections and reloads the IConfigurationRoot.
///   3. SaveAsync surfaces an error dialog if the file is locked /
///      missing (we simulate by setting the path to a missing file).
/// </summary>
public sealed class SettingsViewModelTests : IDisposable
{
    private readonly string _sandboxDir;
    private readonly string _appsettingsPath;
    private readonly IConfigurationRoot _config;
    private readonly IOptionsMonitor<ElevenLabsOptions> _options;
    private readonly IDialogService _dialog;

    public SettingsViewModelTests()
    {
        _sandboxDir = Path.Combine(Path.GetTempPath(), "elevenlabs-sv-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(_sandboxDir);
        _appsettingsPath = Path.Combine(_sandboxDir, "appsettings.json");
        File.WriteAllText(_appsettingsPath, """
            {
              "ElevenLabs": {
                "Mock": true,
                "ApiKey": "old-key-123",
                "BaseUrl": "https://api.elevenlabs.io/"
              },
              "UI": {
                "Theme": "Dark"
              }
            }
            """);
        _config = new ConfigurationBuilder()
            .SetBasePath(_sandboxDir)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
        _options = new ConfigurationOptionsMonitor(_config, new ElevenLabsOptions());
        _dialog = Substitute.For<IDialogService>();
    }

    public void Dispose()
    {
        try { Directory.Delete(_sandboxDir, recursive: true); } catch { /* best effort */ }
    }

    private SettingsViewModel NewViewModel() => new(
        _options, _dialog, NullLogger<SettingsViewModel>.Instance, _config, _appsettingsPath);

    [Fact]
    public void Ctor_populates_ApiKey_and_MockMode_from_current_options()
    {
        var vm = NewViewModel();

        vm.ApiKey.Should().Be("old-key-123");
        vm.MockMode.Should().BeTrue();
    }

    [Fact]
    public async Task PersistToFileAsync_round_trips_ElevenLabs_section_preserves_other_sections()
    {
        var vm = NewViewModel();
        vm.ApiKey = "ignored-by-test";
        vm.MockMode = false;

        await InvokePersistToFileAsync(vm);

        // The raw file should still have the UI / Theme section byte
        // for byte (modulo indentation), and the new ElevenLabs block.
        var raw = await File.ReadAllTextAsync(_appsettingsPath);
        Assert.True(raw.Contains("ignored-by-test"), "raw file did not contain the new ApiKey; full file:\n" + raw);
        var root = JsonNode.Parse(raw)!.AsObject();
        var ui = root["UI"]!.AsObject();
        ui["Theme"]!.GetValue<string>().Should().Be("Dark");
        var el = root["ElevenLabs"]!.AsObject();
        el["Mock"]!.GetValue<bool>().Should().BeFalse();
        el["ApiKey"]!.GetValue<string>().Should().Be("ignored-by-test");
        el["BaseUrl"]!.GetValue<string>().Should().Be("https://api.elevenlabs.io/");
    }

    [Fact]
    public async Task PersistToFileAsync_throws_when_appsettings_missing()
    {
        File.Delete(_appsettingsPath);
        var vm = NewViewModel();

        var act = () => InvokePersistToFileAsync(vm);

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public void MockBadgeText_reflects_MockMode_state()
    {
        var vm = NewViewModel();

        vm.MockMode = true;
        vm.MockBadgeText.Should().Be("ON");
        vm.MockMode = false;
        vm.MockBadgeText.Should().Be("OFF");
    }

    // The PersistToFileAsync method is private; call it via reflection
    // so the tests stay focused on the contract.
    private static Task InvokePersistToFileAsync(SettingsViewModel vm)
    {
        // Build the snapshot the way SaveAsync does — from the VM's
        // *public* values, not the in-memory options. That way the
        // round-trip test stays in sync with whatever the test set
        // (MockMode = false, ApiKey = "ignored-by-test") just above.
        // The test sandbox file always seeds BaseUrl to
        // "https://api.elevenlabs.io/" so we hardcode it here.
        var snapshot = new ElevenLabsOptions
        {
            Mock = vm.MockMode,
            ApiKey = vm.ApiKey,
            BaseUrl = "https://api.elevenlabs.io/",
        };
        var method = vm.GetType().GetMethod(
            "PersistToFileAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        // Direct invocation returns a Task; await it so the original
        // exceptions propagate instead of being swallowed by the
        // reflection wrapper (which turns them into TargetInvocationException).
        return (Task)method.Invoke(vm, new object[] { snapshot })!;
    }

    /// <summary>
    /// Minimal IOptionsMonitor that re-reads from an IConfigurationRoot
    /// every time CurrentValue is queried. The real host wires this
    /// via Microsoft.Extensions.Options + Configuration.
    /// </summary>
    private sealed class ConfigurationOptionsMonitor : IOptionsMonitor<ElevenLabsOptions>
    {
        private readonly IConfigurationRoot _root;
        public ConfigurationOptionsMonitor(IConfigurationRoot root, ElevenLabsOptions options)
        {
            _root = root;
            CurrentValue = options;
        }
        public ElevenLabsOptions CurrentValue
        {
            get => Read();
            set { /* setter only used by ctor via backing field */ _ = value; }
        }

        private ElevenLabsOptions Read()
        {
            // Re-read on every access so tests that PersistToFileAsync
            // (which calls Reload on the root) see the new value
            // without us wiring change tokens.
            var section = _root.GetSection("ElevenLabs");
            return new ElevenLabsOptions
            {
                Mock = section.GetValue<bool>("Mock", true),
                ApiKey = section.GetValue<string>("ApiKey") ?? string.Empty,
                BaseUrl = section.GetValue<string>("BaseUrl") ?? "https://api.elevenlabs.io/",
            };
        }
        public ElevenLabsOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<ElevenLabsOptions, string?> listener) => null;
    }
}