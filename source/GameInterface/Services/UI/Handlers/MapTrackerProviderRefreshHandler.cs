using Common.Messaging;
using GameInterface.Services.UI.Messages;
using GameInterface.Services.UI.Patches;

namespace GameInterface.Services.UI.Handlers;

/// <summary>
/// Handler for resetting the trackers after the mainhero has been properly set up
/// </summary>
internal class MapTrackerProviderRefreshHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IMapTrackerProviderHolder holder;

    public MapTrackerProviderRefreshHandler(
        IMessageBroker messageBroker,
        IMapTrackerProviderHolder holder)
    {
        this.messageBroker = messageBroker;
        this.holder = holder;

        messageBroker.Subscribe<SwitchedPlayer>(Handle_SwitchedPlayer);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<SwitchedPlayer>(Handle_SwitchedPlayer);
    }

    private void Handle_SwitchedPlayer(MessagePayload<SwitchedPlayer> payload)
    {
        if (holder.Current == null) return;
        holder.Current.ResetTrackers();
    }
}