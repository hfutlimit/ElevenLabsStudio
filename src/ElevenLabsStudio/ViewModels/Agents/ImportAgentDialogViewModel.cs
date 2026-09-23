using Caliburn.Micro;
using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.Core.Exceptions;
using ElevenLabsStudio.Core.MVVM;
using Microsoft.Extensions.Logging;

namespace ElevenLabsStudio.ViewModels.Agents;

/// <summary>
/// Tiny dialog VM shown when the user clicks "Import Agent" in the
/// sidebar. Validates a non-empty AgentId, calls
/// <see cref="IElevenLabsClient.GetAgentAsync"/>, and closes itself with
/// a result of <c>true</c> + the imported <see cref="Agent"/> attached
/// to <see cref="Result"/>.
/// </summary>
public sealed class ImportAgentDialogViewModel : ScreenBase
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

	public ImportAgentDialogViewModel(
		IElevenLabsClient client,
		IDialogService dialog,
		ILogger logger)
	{
		_client = client;
		_dialog = dialog;
		_logger = logger;
		DisplayName = "Import Agent by ID";
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
			_logger.LogError(ex, "Auth failed while importing agent {AgentId}", AgentId);
			Error = "The API key is invalid or missing. Configure ElevenLabs.ApiKey in appsettings.json and restart the app.";
		}
		catch (ElevenLabsException ex)
		{
			_logger.LogError(ex, "ElevenLabs error while importing agent {AgentId} (status={Status})", AgentId, ex.HttpStatus);
			Error = $"HTTP {ex.HttpStatus}: {ex.Message}";
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Unexpected error while importing agent {AgentId}", AgentId);
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
