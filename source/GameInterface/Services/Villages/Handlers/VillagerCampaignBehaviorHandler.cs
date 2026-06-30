using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MobileParties.Interfaces;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Villages.Messages;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Villages.Handlers;

internal class VillagerCampaignBehaviorHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<VillagerCampaignBehaviorHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ISessionInteractionsPlayerDataInterface sessionInteractionsPlayerDataInterface;
    private readonly ConversationPartyTracker conversationPartyTracker;

    public VillagerCampaignBehaviorHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ISessionInteractionsPlayerDataInterface sessionInteractionsPlayerDataInterface,
        ConversationPartyTracker conversationPartyTracker)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.sessionInteractionsPlayerDataInterface = sessionInteractionsPlayerDataInterface;
        this.conversationPartyTracker = conversationPartyTracker;

        messageBroker.Subscribe<DeleteExpiredLootedVillagers>(Handle_DeleteExpiredLootedVillagers);
        messageBroker.Subscribe<NetworkDeleteExpiredLootedVillagers>(Handle_NetworkDeleteExpiredLootedVillagers);

        messageBroker.Subscribe<MobilePartyDestroyed>(Handle_MobilePartyDestroyed);
        messageBroker.Subscribe<NetworkVillagerPartyDestroyed>(Handle_NetworkVillagerPartyDestroyed);

        messageBroker.Subscribe<NetworkAddToLootedVillagers>(Handle_NetworkAddToLootedVillagers);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<DeleteExpiredLootedVillagers>(Handle_DeleteExpiredLootedVillagers);
        messageBroker.Unsubscribe<NetworkDeleteExpiredLootedVillagers>(Handle_NetworkDeleteExpiredLootedVillagers);

        messageBroker.Unsubscribe<MobilePartyDestroyed>(Handle_MobilePartyDestroyed);
        messageBroker.Unsubscribe<NetworkVillagerPartyDestroyed>(Handle_NetworkVillagerPartyDestroyed);

        messageBroker.Unsubscribe<NetworkAddToLootedVillagers>(Handle_NetworkAddToLootedVillagers);
    }

    private void Handle_DeleteExpiredLootedVillagers(MessagePayload<DeleteExpiredLootedVillagers> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetVillagerBehavior(out var villagerBehavior)) return;

            // Vanilla implementation, need to send list to clients
            List<MobileParty> list = new List<MobileParty>();
            foreach (KeyValuePair<MobileParty, CampaignTime> keyValuePair in villagerBehavior._lootedVillagers)
            {
                if (CampaignTime.Now - keyValuePair.Value >= CampaignTime.Days(10f))
                {
                    list.Add(keyValuePair.Key);
                }
            }
            foreach (MobileParty key in list)
            {
                villagerBehavior._lootedVillagers.Remove(key);
            }

            // Update changes to _lootedVillagers on clients
            List<string> deletedLootedVillagersIdsList = new();
            foreach (var deletedVillager in list)
            {
                if (!objectManager.TryGetIdWithLogging(deletedVillager, out var villagerPartyId)) continue;

                deletedLootedVillagersIdsList.Add(villagerPartyId);
            }
            network.SendAll(new NetworkDeleteExpiredLootedVillagers(deletedLootedVillagersIdsList));
        });
    }

    private void Handle_NetworkDeleteExpiredLootedVillagers(MessagePayload<NetworkDeleteExpiredLootedVillagers> obj)
    {
        // Update looted villagers on clients
        GameThread.RunSafe(() =>
        {
            if (!TryGetVillagerBehavior(out var villagerBehavior)) return;

            foreach (var deletedVillagerId in obj.What.DeletedLootedVillagersIdsList)
            {
                if (!objectManager.TryGetObjectWithLogging<MobileParty>(deletedVillagerId, out var deletedVillager)) continue;

                villagerBehavior._lootedVillagers.Remove(deletedVillager);
            }
        });
    }

    private void Handle_MobilePartyDestroyed(MessagePayload<MobilePartyDestroyed> obj)
    {
        // Don't process anything for destroyed mobile parties that aren't villagers
        if (!obj.What.MobileParty.IsVillager) return;

        if (!objectManager.TryGetIdWithLogging(obj.What.MobileParty, out var mobilePartyId)) return;

        // Update CoopSession data on server
        sessionInteractionsPlayerDataInterface.RemoveInteractedVillagersForAllPlayers(mobilePartyId);

        var message = new NetworkVillagerPartyDestroyed(mobilePartyId);
        network.SendAll(message);
    }

    private void Handle_NetworkVillagerPartyDestroyed(MessagePayload<NetworkVillagerPartyDestroyed> obj)
    {
        // Update interacted villagers locally on all clients
        GameThread.Run(() =>
        {
            if (!TryGetVillagerBehavior(out var villagerBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MobilePartyId, out var mobileParty)) return;

            if (villagerBehavior._interactedVillagers.ContainsKey(mobileParty))
            {
                villagerBehavior._interactedVillagers.Remove(mobileParty);
            }
        });
    }

    private void Handle_NetworkAddToLootedVillagers(MessagePayload<NetworkAddToLootedVillagers> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetVillagerBehavior(out var villagerBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.VillagersPartyId, out var villagerParty)) return;

            villagerBehavior._lootedVillagers.Add(villagerParty, obj.What.CampaignTime);
        });
    }

    private bool TryGetVillagerBehavior(out VillagerCampaignBehavior villagerBehavior)
    {
        villagerBehavior = Campaign.Current?.GetCampaignBehavior<VillagerCampaignBehavior>();
        if (villagerBehavior != null) return true;

        Logger.Debug("Skipping villager update because VillagerCampaignBehavior is unavailable");
        return false;
    }

    // Used by VillagerCampaignBehaviorPatches.HourlyTickPartyPrefix to check if a villager party
    // is in a conversation with a player using the ConversationPartyTracker
    public bool CanVillagerPartyMove(MobileParty villagerParty)
    {
        if (!objectManager.TryGetIdWithLogging(villagerParty.Party, out var villagerPartyId)) return false;

        return !conversationPartyTracker.IsEngagedByOther(villagerPartyId, null);
    }
}
