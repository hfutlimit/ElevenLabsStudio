using System.Globalization;
using System.Collections.Specialized;
using Caliburn.Micro;
using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.Core.MVVM;
using ElevenLabsStudio.Services;
using Microsoft.Extensions.Logging;

namespace ElevenLabsStudio.ViewModels.AgentDetail;

public sealed class LiveConversationViewModel : ScreenBase, IDisposable
{
	public const string DefaultBranchId = "agtbrch_7101m2hctwtefwtrt0jc1eaw7m9t";
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
	private string _branchId = DefaultBranchId;
	private string _environment = "production";
	private InitialWebhookVariableScenario _selectedVariableScenario;
	private string _messageText = string.Empty;
	private TimeSpan _elapsed;
	private int _dynamicVariableSequence;
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
		Transcript.CollectionChanged += OnTranscriptCollectionChanged;
		_selectedVariableScenario = VariableScenarios[0];
		LoadScenarioVariables(_selectedVariableScenario);
	}

	public BindableCollection<RealtimeTranscriptMessage> Transcript { get; } = new();
	public BindableCollection<DynamicVariableEntry> DynamicVariables { get; } = new();
	public bool HasTranscript => Transcript.Count > 0;
	public bool HasNoTranscript => !HasTranscript;
	public IReadOnlyList<InitialWebhookVariableScenario> VariableScenarios { get; } =
		ReferenceTesterInitialWebhookVariables.Scenarios;

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

	public InitialWebhookVariableScenario SelectedVariableScenario
	{
		get => _selectedVariableScenario;
		set
		{
			ArgumentNullException.ThrowIfNull(value);
			if (!Set(ref _selectedVariableScenario, value)) return;
			LoadScenarioVariables(value);
		}
	}

	public void AddDynamicVariable()
	{
		// A monotonically increasing counter, never Count + 1: removing
		// a middle row used to hand the next Add the very same key back,
		// and CreateOptions then refused the whole session with
		// "Dynamic variable key 'variable_2' is duplicated."
		var index = ++_dynamicVariableSequence;
		DynamicVariables.Add(new DynamicVariableEntry($"variable_{index}", string.Empty));
	}

	public void RemoveDynamicVariable(DynamicVariableEntry? variable)
	{
		if (variable is not null) DynamicVariables.Remove(variable);
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
		catch (Exception ex) when (ex is FormatException)
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
			if (_disposed)
			{
				// Dispose raced the connect: the agent was switched (or
				// the pane torn down) while this await was in flight, so
				// Dispose already ran with a null _session and had
				// nothing to clean up. Storing the late-arriving session
				// on a dead view model would strand the singleton
				// WebViewRealtimeConversationClient in "already active"
				// forever — every later agent got a hard failure. Hand
				// the session straight back instead.
				_logger.LogInformation(
					"Disposing late realtime session for {AgentId}: the view model was disposed while connecting",
					AgentId);
				DisposeDetachedSession(session);
				return;
			}

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
		Transcript.CollectionChanged -= OnTranscriptCollectionChanged;
		StopElapsed();
		Unsubscribe(_session);
		if (_session is not null)
		{
			DisposeDetachedSession(_session);
			_session = null;
		}
	}

	/// <summary>
	/// Fire-and-forget teardown for a session nobody is holding on to.
	/// WebViewRealtimeConversationSession.DisposeAsync already swallows
	/// the InvalidOperationException that fires when the browser is
	/// detached before stop completes — but anything else thrown would
	/// become an unobserved task exception, so route it to the log.
	/// </summary>
	private void DisposeDetachedSession(IRealtimeConversationSession session) =>
		_ = session.DisposeAsync().AsTask().ContinueWith(
			t => _logger.LogError(t.Exception, "Session.DisposeAsync threw for {AgentId}", AgentId),
			CancellationToken.None,
			TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.RunContinuationsAsynchronously,
			TaskScheduler.Default);

	private RealtimeConversationOptions CreateOptions()
	{
		var variables = new Dictionary<string, object?>(StringComparer.Ordinal);
		foreach (var entry in DynamicVariables)
		{
			var key = entry.Key.Trim();
			if (key.Length == 0)
			{
				throw new FormatException("Dynamic variable keys cannot be empty.");
			}

			if (!variables.TryAdd(key, entry.Value))
			{
				throw new FormatException($"Dynamic variable key '{key}' is duplicated.");
			}
		}

		return new RealtimeConversationOptions(
			AgentId,
			string.IsNullOrWhiteSpace(BranchId) ? null : BranchId.Trim(),
			Environment,
			variables);
	}

	private void LoadScenarioVariables(InitialWebhookVariableScenario scenario)
	{
		DynamicVariables.Clear();
		// The collection just went back to empty, so restarting the
		// name sequence can't collide with anything still in it.
		_dynamicVariableSequence = 0;
		foreach (var variable in scenario.Variables)
		{
			DynamicVariables.Add(new DynamicVariableEntry(variable.Key, variable.Value?.ToString() ?? string.Empty));
		}
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

	private void OnTranscriptCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		NotifyOfPropertyChange(nameof(HasTranscript));
		NotifyOfPropertyChange(nameof(HasNoTranscript));
	}

	private void OnVolumeChanged(object? sender, RealtimeConversationVolumeChangedEventArgs e)
	{
		InputVolume = Math.Clamp(e.Input, 0f, 1f);
		OutputVolume = Math.Clamp(e.Output, 0f, 1f);
	}

	private void OnError(object? sender, RealtimeConversationErrorEventArgs e)
	{
		Status = RealtimeConversationStatus.Failed;
		_logger.LogWarning(e.Exception, "Realtime conversation error for {AgentId}", AgentId);
		// OnError is a synchronous event handler, so we can't await here
		// (async void would surface unobserved exceptions on the finalizer).
		// Attach a fault-only continuation so any throw inside the dialog
		// service still lands in the log instead of disappearing.
		_ = ShowFailureAsync("Realtime conversation error", e.Exception.Message, e.Exception)
			.ContinueWith(
				t => _logger.LogError(t.Exception, "ShowFailureAsync threw for {AgentId}", AgentId),
				CancellationToken.None,
				TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.RunContinuationsAsynchronously,
				TaskScheduler.Default);
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

}
