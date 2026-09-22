using System.Globalization;
using System.Text.Json;
using Caliburn.Micro;
using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.Core.MVVM;
using Microsoft.Extensions.Logging;

namespace ElevenLabsStudio.ViewModels.AgentDetail;

public sealed class LiveConversationViewModel : ScreenBase, IDisposable
{
	private readonly Agent _agent;
	private readonly IRealtimeConversationClient _client;
	private readonly IDialogService _dialog;
	private readonly IClockService _clock;
	private readonly ILogger<LiveConversationViewModel> _logger;
	private IRealtimeConversationSession? _session;
	private IDisposable? _elapsedTimer;
	private DateTime? _startedAt;
	private RealtimeConversationStatus _status = RealtimeConversationStatus.Disconnected;
	private RealtimeConversationMode _mode = RealtimeConversationMode.Unknown;
	private string? _conversationId;
	private bool _isMuted;
	private float _inputVolume;
	private float _outputVolume;
	private string _branchId = string.Empty;
	private string _environment = "production";
	private string _dynamicVariablesJson;
	private string _messageText = string.Empty;
	private TimeSpan _elapsed;
	private bool _disposed;

	public LiveConversationViewModel(
		Agent agent,
		IRealtimeConversationClient client,
		IDialogService dialog,
		IClockService clock,
		ILogger<LiveConversationViewModel> logger)
	{
		_agent = agent;
		_client = client;
		_dialog = dialog;
		_clock = clock;
		_logger = logger;
		_dynamicVariablesJson = JsonSerializer.Serialize(
			agent.Variables.ToDictionary(variable => variable.Name, ToRuntimeValue),
			new JsonSerializerOptions { WriteIndented = true });
	}

	public BindableCollection<RealtimeTranscriptMessage> Transcript { get; } = new();

	public string AgentName => _agent.Name;
	public string AgentId => _agent.AgentId;

	public string BranchId
	{
		get => _branchId;
		set => Set(ref _branchId, value);
	}

	public string Environment
	{
		get => _environment;
		set => Set(ref _environment, string.IsNullOrWhiteSpace(value) ? "production" : value.Trim());
	}

	public string DynamicVariablesJson
	{
		get => _dynamicVariablesJson;
		set => Set(ref _dynamicVariablesJson, value);
	}

	public string MessageText
	{
		get => _messageText;
		set => Set(ref _messageText, value);
	}

	public RealtimeConversationStatus Status
	{
		get => _status;
		private set
		{
			if (!Set(ref _status, value)) return;
			NotifyOfPropertyChange(nameof(StatusText));
			NotifyOfPropertyChange(nameof(IsConnected));
			NotifyOfPropertyChange(nameof(CanStart));
			NotifyOfPropertyChange(nameof(CanStop));
		}
	}

	public string StatusText => Status switch
	{
		RealtimeConversationStatus.Disconnected => "Disconnected",
		RealtimeConversationStatus.Connecting => "Connecting",
		RealtimeConversationStatus.Connected => "Connected",
		RealtimeConversationStatus.Disconnecting => "Disconnecting",
		RealtimeConversationStatus.Failed => "Failed",
		_ => "Unknown",
	};

	public RealtimeConversationMode Mode
	{
		get => _mode;
		private set
		{
			if (!Set(ref _mode, value)) return;
			NotifyOfPropertyChange(nameof(ModeText));
		}
	}

	public string ModeText => Mode switch
	{
		RealtimeConversationMode.Listening => "Listening",
		RealtimeConversationMode.Speaking => "Speaking",
		_ => "—",
	};

	public string? ConversationId
	{
		get => _conversationId;
		private set => Set(ref _conversationId, value);
	}

	public bool IsConnected => Status == RealtimeConversationStatus.Connected;
	public bool CanStart => _session is null && !IsBusy;
	public bool CanStop => _session is not null;

	public bool IsMuted
	{
		get => _isMuted;
		private set
		{
			if (!Set(ref _isMuted, value)) return;
			NotifyOfPropertyChange(nameof(MuteButtonText));
		}
	}

	public string MuteButtonText => IsMuted ? "Unmute microphone" : "Mute microphone";

	public float InputVolume
	{
		get => _inputVolume;
		private set => Set(ref _inputVolume, value);
	}

	public float OutputVolume
	{
		get => _outputVolume;
		private set => Set(ref _outputVolume, value);
	}

	public TimeSpan Elapsed
	{
		get => _elapsed;
		private set
		{
			if (!Set(ref _elapsed, value)) return;
			NotifyOfPropertyChange(nameof(ElapsedText));
		}
	}

	public string ElapsedText => string.Format(
		CultureInfo.InvariantCulture,
		"{0:00}:{1:00}",
		(int)Elapsed.TotalMinutes,
		Elapsed.Seconds);

	public async Task StartAsync(CancellationToken ct = default)
	{
		if (_session is not null || IsBusy) return;

		RealtimeConversationOptions options;
		try
		{
			options = CreateOptions();
		}
		catch (Exception ex) when (ex is JsonException or FormatException)
		{
			await ShowFailureAsync("Invalid session data", ex.Message, ex);
			return;
		}

		IsBusy = true;
		BusyMessage = "Connecting to agent…";
		Status = RealtimeConversationStatus.Connecting;
		Transcript.Clear();
		ConversationId = null;
		Mode = RealtimeConversationMode.Unknown;
		try
		{
			var session = await _client.StartAsync(options, ct);
			_session = session;
			Subscribe(session);
			ConversationId = session.ConversationId;
			Status = session.Status;
			if (Status == RealtimeConversationStatus.Connected)
			{
				BeginElapsed();
			}
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			Status = RealtimeConversationStatus.Disconnected;
			throw;
		}
		catch (Exception ex)
		{
			await ShowFailureAsync("Could not start conversation", ex.Message, ex);
			Status = RealtimeConversationStatus.Failed;
		}
		finally
		{
			IsBusy = false;
			BusyMessage = null;
			NotifyOfPropertyChange(nameof(CanStart));
			NotifyOfPropertyChange(nameof(CanStop));
		}
	}

	public async Task StopAsync(CancellationToken ct = default)
	{
		var session = _session;
		if (session is null) return;

		Status = RealtimeConversationStatus.Disconnecting;
		try
		{
			await session.StopAsync(ct);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogWarning(ex, "Realtime conversation stop failed for {AgentId}", AgentId);
			await ShowFailureAsync("Could not end conversation", ex.Message, ex);
			HandleDisconnected();
		}
	}

	public async Task ToggleMuteAsync(CancellationToken ct = default)
	{
		if (_session is null) return;
		var muted = !IsMuted;
		try
		{
			await _session.SetMutedAsync(muted, ct);
			IsMuted = muted;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			await ShowFailureAsync("Could not change microphone state", ex.Message, ex);
		}
	}

	public async Task SendTextAsync(CancellationToken ct = default)
	{
		var text = MessageText.Trim();
		if (_session is null || text.Length == 0) return;

		try
		{
			await _session.SendTextAsync(text, ct);
			MessageText = string.Empty;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			await ShowFailureAsync("Could not send message", ex.Message, ex);
		}
	}

	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;
		StopElapsed();
		Unsubscribe(_session);
		if (_session is not null)
		{
			_ = _session.DisposeAsync();
			_session = null;
		}
	}

	private RealtimeConversationOptions CreateOptions()
	{
		using var document = JsonDocument.Parse(DynamicVariablesJson);
		if (document.RootElement.ValueKind != JsonValueKind.Object)
		{
			throw new JsonException("Dynamic variables must be a JSON object.");
		}

		var variables = document.RootElement.EnumerateObject()
			.ToDictionary(property => property.Name, property => ConvertJsonValue(property.Value));
		return new RealtimeConversationOptions(
			AgentId,
			string.IsNullOrWhiteSpace(BranchId) ? null : BranchId.Trim(),
			Environment,
			variables);
	}

	private void Subscribe(IRealtimeConversationSession session)
	{
		session.StatusChanged += OnStatusChanged;
		session.ModeChanged += OnModeChanged;
		session.TranscriptReceived += OnTranscriptReceived;
		session.VolumeChanged += OnVolumeChanged;
		session.Error += OnError;
	}

	private void Unsubscribe(IRealtimeConversationSession? session)
	{
		if (session is null) return;
		session.StatusChanged -= OnStatusChanged;
		session.ModeChanged -= OnModeChanged;
		session.TranscriptReceived -= OnTranscriptReceived;
		session.VolumeChanged -= OnVolumeChanged;
		session.Error -= OnError;
	}

	private void OnStatusChanged(object? sender, RealtimeConversationStatusChangedEventArgs e)
	{
		Status = e.Status;
		ConversationId = e.ConversationId ?? ConversationId;
		if (e.Status == RealtimeConversationStatus.Connected)
		{
			IsBusy = false;
			BeginElapsed();
		}
		else if (e.Status is RealtimeConversationStatus.Disconnected or RealtimeConversationStatus.Failed)
		{
			HandleDisconnected();
		}
	}

	private void OnModeChanged(object? sender, RealtimeConversationModeChangedEventArgs e) => Mode = e.Mode;

	private void OnTranscriptReceived(object? sender, RealtimeTranscriptEventArgs e) => Transcript.Add(e.Message);

	private void OnVolumeChanged(object? sender, RealtimeConversationVolumeChangedEventArgs e)
	{
		InputVolume = Math.Clamp(e.Input, 0f, 1f);
		OutputVolume = Math.Clamp(e.Output, 0f, 1f);
	}

	private void OnError(object? sender, RealtimeConversationErrorEventArgs e)
	{
		Status = RealtimeConversationStatus.Failed;
		_logger.LogWarning(e.Exception, "Realtime conversation error for {AgentId}", AgentId);
		_ = ShowFailureAsync("Realtime conversation error", e.Exception.Message, e.Exception);
	}

	private void HandleDisconnected()
	{
		StopElapsed();
		IsBusy = false;
		Unsubscribe(_session);
		_session = null;
		IsMuted = false;
		NotifyOfPropertyChange(nameof(CanStart));
		NotifyOfPropertyChange(nameof(CanStop));
	}

	private void BeginElapsed()
	{
		if (_elapsedTimer is not null) return;
		_startedAt ??= _clock.Now;
		_elapsedTimer = _clock.Start(TimeSpan.FromMilliseconds(500), UpdateElapsed);
	}

	private void UpdateElapsed()
	{
		if (_startedAt is { } startedAt)
		{
			Elapsed = _clock.Now - startedAt;
		}
	}

	private void StopElapsed()
	{
		_elapsedTimer?.Dispose();
		_elapsedTimer = null;
		_startedAt = null;
		Elapsed = TimeSpan.Zero;
	}

	private async Task ShowFailureAsync(string title, string message, Exception exception)
	{
		_logger.LogWarning(exception, "{Title} for realtime conversation {AgentId}", title, AgentId);
		await _dialog.ShowErrorAsync(title, message);
	}

	private static object? ConvertJsonValue(JsonElement value) => value.ValueKind switch
	{
		JsonValueKind.String => value.GetString(),
		JsonValueKind.Number when value.TryGetInt64(out var integer) => integer,
		JsonValueKind.Number when value.TryGetDecimal(out var decimalValue) => decimalValue,
		JsonValueKind.True => true,
		JsonValueKind.False => false,
		JsonValueKind.Null => null,
		_ => value.Clone(),
	};

	private static object ToRuntimeValue(Variable variable)
	{
		var value = variable.Value ?? string.Empty;
		if (string.Equals(variable.Type, "number", StringComparison.OrdinalIgnoreCase)
			&& decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
		{
			return number;
		}
		if (string.Equals(variable.Type, "boolean", StringComparison.OrdinalIgnoreCase)
			&& bool.TryParse(value, out var boolean))
		{
			return boolean;
		}
		return value;
	}
}
