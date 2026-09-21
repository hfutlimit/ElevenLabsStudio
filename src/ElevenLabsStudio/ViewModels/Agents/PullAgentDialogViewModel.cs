using Caliburn.Micro;
using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.Core.Exceptions;
using ElevenLabsStudio.Core.MVVM;
using Microsoft.Extensions.Logging;

namespace ElevenLabsStudio.ViewModels.Agents;

/// <summary>
/// Tiny dialog VM shown when the user clicks "📥 拉取 Agent" in the
/// sidebar. Validates a non-empty AgentId, calls
/// <see cref="IElevenLabsClient.GetAgentAsync"/>, and closes itself with
/// a result of <c>true</c> + the pulled <see cref="Agent"/> attached
/// to <see cref="Result"/>.
/// </summary>
public sealed class PullAgentDialogViewModel : ScreenBase
{
	private readonly IElevenLabsClient _client;
	private readonly IDialogService _dialog;
	private readonly ILogger _logger;

	private string _agentId = string.Empty;
	public string AgentId
	{
		get => _agentId;
		set { if (Set(ref _agentId, value)) NotifyOfPropertyChange(nameof(CanConfirm)); }
	}

	private string? _error;
	public string? Error
	{
		get => _error;
		private set
		{
			if (Set(ref _error, value))
			{
				NotifyOfPropertyChange(nameof(HasError));
			}
		}
	}

	public bool HasError => !string.IsNullOrEmpty(_error);

	public bool CanConfirm => !string.IsNullOrWhiteSpace(_agentId) && !IsBusy;

	public Agent? Result { get; private set; }

	public PullAgentDialogViewModel(
		IElevenLabsClient client,
		IDialogService dialog,
		ILogger logger)
	{
		_client = client;
		_dialog = dialog;
		_logger = logger;
		DisplayName = "按 ID 拉取 Agent";
	}

	public async Task Confirm()
	{
		Error = null;
		IsBusy = true;
		try
		{
			Result = await _client.GetAgentAsync(AgentId.Trim());
			await TryCloseAsync(true);
		}
		catch (ElevenLabsAuthException ex)
		{
			_logger.LogError(ex, "Auth failed while pulling agent {AgentId}", AgentId);
			Error = "API key 无效或缺失。请在 appsettings.json 配置 ElevenLabs.ApiKey 后重启。";
		}
		catch (ElevenLabsException ex)
		{
			_logger.LogError(ex, "ElevenLabs error while pulling agent {AgentId} (status={Status})", AgentId, ex.HttpStatus);
			Error = $"HTTP {ex.HttpStatus}: {ex.Message}";
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Unexpected error while pulling agent {AgentId}", AgentId);
			Error = ex.Message;
		}
		finally
		{
			IsBusy = false;
			NotifyOfPropertyChange(nameof(CanConfirm));
			NotifyOfPropertyChange(nameof(BusyMessage));
		}
	}

	public Task Cancel() => TryCloseAsync(false);
}
