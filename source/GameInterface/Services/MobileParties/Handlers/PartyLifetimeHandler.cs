using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.GameDebug.Handlers;
using GameInterface.Services.MobileParties.Messages.Lifetime;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Reflection;
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
        messageBroker.Subscribe<PartyCreated>(Handle_PartyCreated);
        messageBroker.Subscribe<NetworkCreateParty>(Handle_CreateParty);
        messageBroker.Subscribe<PartyDestroyed>(Handle_PartyDestroyed);
        messageBroker.Subscribe<NetworkDestroyParty>(Handle_DestroyParty);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PartyCreated>(Handle_PartyCreated);
        messageBroker.Unsubscribe<NetworkCreateParty>(Handle_CreateParty);
        messageBroker.Unsubscribe<PartyDestroyed>(Handle_PartyDestroyed);
        messageBroker.Unsubscribe<NetworkDestroyParty>(Handle_DestroyParty);
    }

    private void Handle_PartyCreated(MessagePayload<PartyCreated> payload)
    {
        var party = payload.What.Instance;

        if (objectManager.AddNewObject(party, out var newId) == false) return;

        network.SendAll(new NetworkCreateParty(newId));
    }

    private readonly ConstructorInfo MobileParty_ctor = AccessTools.Constructor(typeof(MobileParty));
    private void Handle_CreateParty(MessagePayload<NetworkCreateParty> payload)
    {
        var stringId = payload.What.StringId;

        var isClient = ModInformation.IsClient ? "Client" : "Server";

        Logger.Debug("Creating party {partyId} for {instance}", stringId, isClient);

        var newParty = ObjectHelper.SkipConstructor<MobileParty>();

        if (objectManager.AddExisting(stringId, newParty) == false)
        {
            Logger.Error("Failed to create party with id {stringId}", stringId);
            return;
        }
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
        var stringId = payload.What.StringId;

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
