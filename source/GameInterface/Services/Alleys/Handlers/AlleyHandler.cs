using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Alleys.Messages;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Alleys.Handlers;

/// <summary>
/// Replicates alley owner changes. On the server it turns the local
/// <see cref="AlleyOwnerChanged"/> event into a networked <see cref="ChangeAlleyOwner"/>
/// broadcast; on each client it replays <c>Alley.SetOwner</c> so the owner, the derived
/// <c>State</c> and the owner's <c>OwnedAlleys</c> list all stay consistent.
/// </summary>
internal class AlleyHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public AlleyHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<AlleyOwnerChanged>(Handle_AlleyOwnerChanged);
        messageBroker.Subscribe<ChangeAlleyOwner>(Handle_ChangeAlleyOwner);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<AlleyOwnerChanged>(Handle_AlleyOwnerChanged);
        messageBroker.Unsubscribe<ChangeAlleyOwner>(Handle_ChangeAlleyOwner);
    }

    private void Handle_AlleyOwnerChanged(MessagePayload<AlleyOwnerChanged> payload)
    {
        if (ModInformation.IsClient) return;

        var data = payload.What;

        if (!objectManager.TryGetIdWithLogging(data.Alley, out var alleyId)) return;

        // A null new owner (alley vacated) is valid and serializes as a null id.
        string newOwnerId = null;
        if (data.NewOwner != null && !objectManager.TryGetIdWithLogging(data.NewOwner, out newOwnerId)) return;

        network.SendAll(new ChangeAlleyOwner(alleyId, newOwnerId));
    }

    private void Handle_ChangeAlleyOwner(MessagePayload<ChangeAlleyOwner> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;

        // Resolve ids inside the game-thread closure: the alley/hero may be registered by an earlier
        // handler that also defers to the game thread, so a poll-thread lookup could miss it.
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Alley>(data.AlleyId, out var alley)) return;

            Hero newOwner = null;
            if (data.NewOwnerId != null && !objectManager.TryGetObjectWithLogging(data.NewOwnerId, out newOwner)) return;

            // Receive/apply path: replay SetOwner with patches stood down so it doesn't re-announce.
            using (new AllowedThread())
            {
                alley.SetOwner(newOwner);
            }
        });
    }
}
