using System.ComponentModel;
using System.Windows.Data;
using Caliburn.Micro;
using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.Core.Events;
using ElevenLabsStudio.Core.Exceptions;
using ElevenLabsStudio.Core.MVVM;
using ElevenLabsStudio.ViewModels.AgentDetail;
using Microsoft.Extensions.Logging;

namespace ElevenLabsStudio.ViewModels.Agents;

/// <summary>
/// Left-hand agent menu. Holds the list of imported agents and the
/// currently selected <see cref="AgentDetailViewModel"/> that the right
/// pane renders. Agents are imported by ID one at a time via
/// <see cref="ImportAgentAsync"/>; the user can refresh an
/// already-imported agent from its detail view.
/// </summary>
public sealed class AgentListViewModel : ScreenBase, IHandle<AgentUpdatedEvent>, IDisposable
{
	private readonly IElevenLabsClient _client;
	private readonly IDialogService _dialog;
	private readonly IEventAggregator _events;
	private readonly Core.Abstractions.IDraftStore _drafts;
	private readonly IAgentDetailViewModelFactory _detailFactory;
	private readonly ILogger<AgentListViewModel> _logger;
	private readonly ISuggestionEngine _suggestions;
	private readonly IWindowManager _windowManager;
	private bool _subscribed;
	private CancellationTokenSource? _selectionCts;
	private long _selectionGeneration;

	public BindableCollection<AgentSummary> Agents { get; } = new();

	/// <summary>Sorted view of <see cref="Agents"/> for the ListBox.</summary>
	public ICollectionView AgentsView { get; }

	private AgentSummary? _selectedAgent;
	public AgentSummary? SelectedAgent
	{
		get => _selectedAgent;
		set => _ = SelectAgentGuardedAsync(value);
	}

	public AgentDetailViewModel? AgentDetail { get; private set; }

	public bool HasNoAgents => Agents.Count == 0;

	public AgentListViewModel(
		IElevenLabsClient client,
		IDialogService dialog,
		IEventAggregator events,
		Core.Abstractions.IDraftStore drafts,
		IAgentDetailViewModelFactory detailFactory,
		ILogger<AgentListViewModel> logger,
		ISuggestionEngine suggestions,
		IWindowManager windowManager)
	{
		_client = client;
		_dialog = dialog;
		_events = events;
		_drafts = drafts;
		_detailFactory = detailFactory;
		_logger = logger;
		_suggestions = suggestions;
		_windowManager = windowManager;

		// Subscribe to the cross-VM notification channel. HandleAsync
		// (below) updates our local snapshot in place, so the
		// sidebar's stale copy of a freshly-pushed agent never lingers
		// after a successful update. Unsubscription happens in Dispose.
		_events.SubscribeOnUIThread(this);
		_subscribed = true;

		AgentsView = CollectionViewSource.GetDefaultView(Agents);

		Agents.CollectionChanged += (_, _) =>
			NotifyOfPropertyChange(nameof(HasNoAgents));
	}

	/// <summary>
	/// Unsubscribe from the event aggregator so the singleton VM
	/// doesn't leak a handler back to itself after the host tears
	/// down. CMs <c>BootstrapperBase</c> previously owned this
	/// lifetime; we now manage it ourselves because the VM is
	/// created via MS DI as a singleton.
	/// </summary>
	public void Dispose()
	{
		AgentDetail?.Dispose();
		_selectionCts?.Cancel();
		_selectionCts?.Dispose();
		_selectionCts = null;
		if (!_subscribed) return;
		// CM5 IEventAggregator only exposes Unsubscribe(object) +
		// UnsubscribeAll(); the per-thread variant lives on the
		// IHandle<T> extensions, not the aggregator itself.
		_events.Unsubscribe(this);
		_subscribed = false;
		_logger.LogDebug("AgentListViewModel unsubscribed from event aggregator");
	}

	/// <summary>
	/// Open the import-by-id dialog. On success, append the agent to the
	/// local list and select it so the right pane renders its detail.
	/// </summary>
	public async Task ImportAgentAsync()
	{
		var dialogVm = new ImportAgentDialogViewModel(_client, _dialog, _logger);
		var ok = await _windowManager.ShowDialogAsync(dialogVm);
		if (ok == true && dialogVm.Result is { } imported)
		{
			var summary = ToSummary(imported);
			// Replace if already present (re-import).
			var idx = Agents.IndexOf(Agents.FirstOrDefault(a => a.AgentId == imported.AgentId)!);
			if (idx >= 0)
			{
				Agents[idx] = summary;
			}
			else
			{
				Agents.Add(summary);
			}
			await SelectAgentAsync(summary);
			await _events.PublishOnUIThreadAsync(
				new AgentListRefreshedEvent(Agents.ToList()));
		}
	}

	public Task HandleAsync(AgentUpdatedEvent message, CancellationToken ct)
	{
		var existing = Agents.FirstOrDefault(a => a.AgentId == message.AgentId);
		if (existing is not null)
		{
			var idx = Agents.IndexOf(existing);
			if (idx >= 0)
			{
				Agents[idx] = ToSummary(message.Snapshot, existing.CreatedAt);
			}
		}
		return Task.CompletedTask;
	}

	/// <summary>
	/// Loads the agent list from the (mock or real) client and populates
	/// the bound collection. Already wired up to run from the ctor so
	/// the UI never sits empty.
	/// </summary>
	public async Task LoadAsync(CancellationToken ct = default)
	{
		IsBusy = true;
		BusyMessage = "Loading agents…";
		try
		{
			var selectedId = _selectedAgent?.AgentId;
			var items = await _client.ListAgentsAsync(ct);
			Agents.Clear();
			Agents.AddRange(items);

			// Auto-select the first agent so the right pane shows the
			// 4-tab detail immediately. The user can pick another
			// one to switch.
			var next = selectedId is null
				? Agents.FirstOrDefault()
				: Agents.FirstOrDefault(agent => agent.AgentId == selectedId)
					?? Agents.FirstOrDefault();
			await SelectAgentAsync(next, ct);

			await _events.PublishOnUIThreadAsync(
				new AgentListRefreshedEvent(items));
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			throw;
		}
		catch (ElevenLabsAuthException ex)
		{
			_logger.LogError(ex, "ElevenLabs auth failed while loading agents");
			await _dialog.ShowErrorAsync(
				"Authentication failed",
				"The API key is invalid or missing. Configure ElevenLabs.ApiKey in appsettings.json and restart the app.");
		}
		catch (ElevenLabsException ex)
		{
			_logger.LogError(ex, "ElevenLabs error while loading agents (status={Status})", ex.HttpStatus);
			await _dialog.ShowErrorAsync("Load failed", $"Could not load agents (HTTP {ex.HttpStatus}).");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Unexpected error while loading agents");
			await _dialog.ShowErrorAsync("Unexpected error", ex.Message);
		}
		finally
		{
			IsBusy = false;
			NotifyOfPropertyChange(nameof(BusyMessage));
		}
	}

	public async Task SelectAgentAsync(
		AgentSummary? summary,
		CancellationToken ct = default)
	{
		if (!Set(ref _selectedAgent, summary))
		{
			return;
		}

		if (AgentDetail is { IsDirty: true } previous)
		{
			_logger.LogInformation(
				"Saving draft for {AgentId} before switching to {NewAgentId}",
				previous.Agent.AgentId,
				summary?.AgentId);
			previous.SaveDraft();
		}
		AgentDetail?.Dispose();

		_selectionCts?.Cancel();
		_selectionCts?.Dispose();
		_selectionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
		var selectionToken = _selectionCts.Token;
		var generation = Interlocked.Increment(ref _selectionGeneration);

		AgentDetail = null;
		NotifyOfPropertyChange(nameof(AgentDetail));
		if (summary is null)
		{
			return;
		}

		try
		{
			var fullAgent = await _client.GetAgentAsync(summary.AgentId, selectionToken);
			if (generation != _selectionGeneration
				|| _selectedAgent?.AgentId != summary.AgentId
				|| selectionToken.IsCancellationRequested)
			{
				return;
			}

			var next = _detailFactory.Create(fullAgent);
			if (next.TryRestoreDraft())
			{
				_logger.LogInformation("Restored draft for {AgentId}", fullAgent.AgentId);
			}
			AgentDetail = next;
			NotifyOfPropertyChange(nameof(AgentDetail));
			await next.InitializeAsync(selectionToken);
		}
		catch (OperationCanceledException) when (selectionToken.IsCancellationRequested)
		{
		}
		catch (ElevenLabsAuthException ex)
		{
			if (selectionToken.IsCancellationRequested || generation != _selectionGeneration) return;
			_logger.LogError(ex, "ElevenLabs auth failed while loading agent {AgentId}", summary.AgentId);
			await _dialog.ShowErrorAsync("Authentication failed", "The API key is invalid or missing.", ct);
		}
		catch (ElevenLabsException ex)
		{
			if (selectionToken.IsCancellationRequested || generation != _selectionGeneration) return;
			_logger.LogError(ex, "ElevenLabs error loading agent {AgentId}", summary.AgentId);
			await _dialog.ShowErrorAsync("Load failed", $"Could not load agent (HTTP {ex.HttpStatus}).", ct);
		}
	}

	private async Task SelectAgentGuardedAsync(AgentSummary? summary)
	{
		try
		{
			await SelectAgentAsync(summary);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Unexpected selection failure for {AgentId}", summary?.AgentId);
			await _dialog.ShowErrorAsync("Unexpected error", ex.Message);
		}
	}

	private static AgentSummary ToSummary(Agent agent, DateTimeOffset? createdAt = null) =>
		new(agent.AgentId, agent.Name, agent.VoiceId, createdAt ?? agent.UpdatedAt);
}
