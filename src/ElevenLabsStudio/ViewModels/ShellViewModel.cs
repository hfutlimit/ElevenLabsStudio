using Caliburn.Micro;
using ElevenLabsStudio.Core.Events;
using ElevenLabsStudio.ViewModels.Agents;
using ElevenLabsStudio.ViewModels.Conversations;

namespace ElevenLabsStudio.ViewModels;

/// <summary>
/// Top-level shell. Holds the two child VMs (Agents / Conversations) and
/// surfaces the active one to a <c>ContentControl</c> in <c>ShellView</c>
/// through the <see cref="ActiveItem"/> property. We model it as a plain
/// <see cref="Screen"/> rather than <c>Conductor&lt;Screen&gt;.Collection.OneActive</c>
/// because the latter requires the inherited lifecycle hooks to be
/// wired up correctly before the XAML root is bound; this keeps the
/// tab-switch logic explicit and easy to follow.
/// </summary>
public sealed class ShellViewModel : Screen, IHandle<AgentUpdatedEvent>
{
    private readonly IEventAggregator _events;
    private readonly AgentListViewModel _agents;
    private readonly ConversationListViewModel _conversations;

    private Screen? _activeItem;
    public Screen? ActiveItem
    {
        get => _activeItem;
        set => Set(ref _activeItem, value);
    }

    public string BusyMessage => (ActiveItem as Core.MVVM.ScreenBase)?.BusyMessage ?? string.Empty;

    public bool IsBusy => (ActiveItem as Core.MVVM.ScreenBase)?.IsBusy ?? false;

    public ShellViewModel(
        IEventAggregator events,
        AgentListViewModel agents,
        ConversationListViewModel conversations)
    {
        _events = events;
        _agents = agents;
        _conversations = conversations;

        ActiveItem = _agents;
    }

    protected override void OnViewLoaded(object view)
    {
        _events.SubscribeOnPublishedThread(this);
        base.OnViewLoaded(view);
    }

    public void ShowAgentsTab()
    {
        ActiveItem = _agents;
        NotifyOfPropertyChange(nameof(BusyMessage));
        NotifyOfPropertyChange(nameof(IsBusy));
    }

    public void ShowConversationsTab()
    {
        ActiveItem = _conversations;
        NotifyOfPropertyChange(nameof(BusyMessage));
        NotifyOfPropertyChange(nameof(IsBusy));
    }

    public Task HandleAsync(AgentUpdatedEvent message, CancellationToken ct) =>
        // Hook left in place for future cross-tab invalidation; currently
        // the Agents tab refreshes its own list, so there is nothing to do.
        Task.CompletedTask;
}