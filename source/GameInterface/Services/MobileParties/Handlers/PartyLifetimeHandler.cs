using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MobileParties.Messages.Lifetime;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Handlers;

/// <summary>
/// Lifetime handler for parties
/// </summary>
internal class PartyLifetimeHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyLifetimeHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public PartyLifetimeHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network) 
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        messageBroker.Subscribe<PartyDestroyed>(Handle_PartyDestroyed);
        messageBroker.Subscribe<NetworkDestroyParty>(Handle_DestroyParty);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PartyDestroyed>(Handle_PartyDestroyed);
        messageBroker.Unsubscribe<NetworkDestroyParty>(Handle_DestroyParty);
    }

    private void Handle_PartyDestroyed(MessagePayload<PartyDestroyed> payload)
    {
        var party = payload.What.Instance;

        if (objectManager.TryGetId(party, out var id) == false) return;

        if (objectManager.Remove(party) == false)
        {
            Logger.Error("Unable to remove party with id {id}", id);
            return;
        }

        network.SendAll(new NetworkDestroyParty(id));
    }

    private void Handle_DestroyParty(MessagePayload<NetworkDestroyParty> payload)
    {
        var stringId = payload.What.PartyId;

        var isClient = ModInformation.IsClient ? "Client" : "Server";

        Logger.Debug("Destroying party {partyId} for {instance}", stringId, isClient);

        if (objectManager.TryGetObject<MobileParty>(stringId, out var party) == false) return;

        if (objectManager.Remove(party) == false)
        {
            // No return as we want to try to remove the rest on the client
            Logger.Error("Failed to remove party {partyId} from object manager", party.StringId);
        }

        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                try
                {
                    party.RemoveParty();
                }
                catch (Exception e)
                {
                    Logger.Error("Unable to remove party with exception {ex}", e);
                }
            }
        });
    }
}
