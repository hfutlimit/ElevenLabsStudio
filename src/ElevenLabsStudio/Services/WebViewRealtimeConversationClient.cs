using System.IO;
using System.Text.Json;
using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Core.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace ElevenLabsStudio.Services;

public sealed class WebViewRealtimeConversationClient : IRealtimeConversationClient, IDisposable
{
	private const string VirtualHostName = "elevenlabs-studio.local";
	private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(15);
	private readonly IRealtimeSessionCredentialProvider _credentials;
	private readonly ILogger<WebViewRealtimeConversationClient> _logger;
	private readonly SemaphoreSlim _gate = new(1, 1);
	private WebView2? _webView;
	private TaskCompletionSource<bool>? _ready;
	private WebViewRealtimeConversationSession? _session;
	private bool _disposed;

	public WebViewRealtimeConversationClient(
		IRealtimeSessionCredentialProvider credentials,
		ILogger<WebViewRealtimeConversationClient> logger)
	{
		_credentials = credentials;
		_logger = logger;
	}

	public async Task AttachAsync(WebView2 webView, CancellationToken ct = default)
	{
		ArgumentNullException.ThrowIfNull(webView);
		if (_webView == webView && _ready?.Task.IsCompletedSuccessfully == true) return;

		await _gate.WaitAsync(ct);
		Task readyTask;
		try
		{
			if (_webView == webView && _ready?.Task.IsCompletedSuccessfully == true) return;
			DetachCore();
			_webView = webView;
			_ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
			readyTask = _ready.Task;
			webView.WebMessageReceived += OnWebMessageReceived;
			await webView.EnsureCoreWebView2Async();
			var clientPath = Path.Combine(AppContext.BaseDirectory, "RealtimeClient", "index.html");
			if (!File.Exists(clientPath))
			{
				throw new FileNotFoundException("Realtime client assets are missing.", clientPath);
			}
			webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
				VirtualHostName,
				Path.GetDirectoryName(clientPath)!,
				CoreWebView2HostResourceAccessKind.Allow);
			webView.CoreWebView2.PermissionRequested += OnPermissionRequested;
			webView.CoreWebView2.Navigate($"https://{VirtualHostName}/index.html");
		}
		catch
		{
			DetachCore();
			throw;
		}
		finally
		{
			_gate.Release();
		}

		await readyTask.WaitAsync(ReadyTimeout, ct);
	}

	public async Task<IRealtimeConversationSession> StartAsync(
		RealtimeConversationOptions options,
		CancellationToken ct = default)
	{
		ArgumentNullException.ThrowIfNull(options);
		await EnsureReadyAsync(ct);
		if (_session is not null)
		{
			throw new InvalidOperationException("A realtime conversation is already active.");
		}

		var signedUrl = await _credentials.GetSignedUrlAsync(options, ct);
		var session = new WebViewRealtimeConversationSession(PostCommandAsync);
		_session = session;
		try
		{
			await PostCommandAsync(new
			{
				type = "start",
				signedUrl,
				dynamicVariables = options.DynamicVariables,
			});
			return session;
		}
		catch
		{
			_session = null;
			await session.DisposeAsync();
			throw;
		}
	}

	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;
		DetachCore();
		_gate.Dispose();
	}

	private async Task EnsureReadyAsync(CancellationToken ct)
	{
		if (_ready is null || _webView is null)
		{
			throw new InvalidOperationException(
				"The realtime audio view is not loaded. Open the Live conversation tab first.");
		}
		await _ready.Task.WaitAsync(ReadyTimeout, ct);
	}

	private Task PostCommandAsync(object command)
	{
		if (_webView?.CoreWebView2 is null)
		{
			throw new InvalidOperationException("The realtime audio view is not ready.");
		}
		var json = JsonSerializer.Serialize(command);
		_webView.CoreWebView2.PostWebMessageAsString(json);
		return Task.CompletedTask;
	}

	private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
	{
		try
		{
			var message = RealtimeBrowserMessageParser.Parse(e.TryGetWebMessageAsString());
			if (message.Type == "ready")
			{
				_ready?.TrySetResult(true);
				return;
			}
			_session?.Handle(message);
			if (message.Status is RealtimeConversationStatus.Disconnected or RealtimeConversationStatus.Failed)
			{
				_session = null;
			}
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Invalid realtime browser message received");
			_session?.Handle(new RealtimeBrowserMessage(
				"error",
				Status: RealtimeConversationStatus.Failed,
				Error: "Realtime audio client returned an invalid message."));
		}
	}

	private static void OnPermissionRequested(
		object? sender,
		CoreWebView2PermissionRequestedEventArgs e)
	{
		if (e.PermissionKind == CoreWebView2PermissionKind.Microphone)
		{
			e.State = CoreWebView2PermissionState.Allow;
		}
	}

	private void DetachCore()
	{
		if (_webView is not null)
		{
			_webView.WebMessageReceived -= OnWebMessageReceived;
			if (_webView.CoreWebView2 is not null)
			{
				_webView.CoreWebView2.PermissionRequested -= OnPermissionRequested;
			}
		}
		_session = null;
		_webView = null;
		_ready?.TrySetCanceled();
		_ready = null;
	}

	private sealed class WebViewRealtimeConversationSession(
		Func<object, Task> postCommand) : IRealtimeConversationSession
	{
		private readonly Func<object, Task> _postCommand = postCommand;
		private bool _disposed;

		public string? ConversationId { get; private set; }
		public RealtimeConversationStatus Status { get; private set; } = RealtimeConversationStatus.Connecting;
		public bool IsMuted { get; private set; }

		public event EventHandler<RealtimeConversationStatusChangedEventArgs>? StatusChanged;
		public event EventHandler<RealtimeConversationModeChangedEventArgs>? ModeChanged;
		public event EventHandler<RealtimeTranscriptEventArgs>? TranscriptReceived;
		public event EventHandler<RealtimeConversationVolumeChangedEventArgs>? VolumeChanged;
		public event EventHandler<RealtimeConversationErrorEventArgs>? Error;

		public Task SendTextAsync(string text, CancellationToken ct = default)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(text);
			ct.ThrowIfCancellationRequested();
			return _postCommand(new { type = "sendText", text = text.Trim() });
		}

		public Task SetMutedAsync(bool muted, CancellationToken ct = default)
		{
			ct.ThrowIfCancellationRequested();
			IsMuted = muted;
			return _postCommand(new { type = "mute", muted });
		}

		public Task StopAsync(CancellationToken ct = default)
		{
			ct.ThrowIfCancellationRequested();
			if (Status is RealtimeConversationStatus.Disconnected or RealtimeConversationStatus.Disconnecting)
			{
				return Task.CompletedTask;
			}
			UpdateStatus(RealtimeConversationStatus.Disconnecting);
			return _postCommand(new { type = "stop" });
		}

		public async ValueTask DisposeAsync()
		{
			if (_disposed) return;
			_disposed = true;
			try
			{
				await StopAsync();
			}
			catch (InvalidOperationException)
			{
				// The browser may already be detached during application shutdown.
			}
		}

		public void Handle(RealtimeBrowserMessage message)
		{
			if (message.ConversationId is not null)
			{
				ConversationId = message.ConversationId;
			}
			if (message.Status is { } status)
			{
				UpdateStatus(status);
			}
			if (message.Mode is { } mode)
			{
				ModeChanged?.Invoke(this, new RealtimeConversationModeChangedEventArgs(mode));
			}
			if (message.Transcript is { } transcript)
			{
				TranscriptReceived?.Invoke(this, new RealtimeTranscriptEventArgs(transcript));
			}
			if (message.InputVolume is not null || message.OutputVolume is not null)
			{
				var input = message.InputVolume ?? 0f;
				var output = message.OutputVolume ?? 0f;
				VolumeChanged?.Invoke(this, new RealtimeConversationVolumeChangedEventArgs(input, output));
			}
			if (!string.IsNullOrWhiteSpace(message.Error))
			{
				Error?.Invoke(this, new RealtimeConversationErrorEventArgs(new InvalidOperationException(message.Error)));
			}
		}

		private void UpdateStatus(RealtimeConversationStatus status)
		{
			Status = status;
			StatusChanged?.Invoke(this, new RealtimeConversationStatusChangedEventArgs(status, ConversationId));
		}
	}
}
