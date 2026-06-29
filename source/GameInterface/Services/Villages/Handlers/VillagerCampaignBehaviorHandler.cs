using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
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

    public VillagerCampaignBehaviorHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ISessionInteractionsPlayerDataInterface sessionInteractionsPlayerDataInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.sessionInteractionsPlayerDataInterface = sessionInteractionsPlayerDataInterface;

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
        var villagersBehavior = GetVillagerBehavior();
        GameThread.RunSafe(() =>
        {
            // Vanilla implementation, need to send list to clients
            List<MobileParty> list = new List<MobileParty>();
            foreach (KeyValuePair<MobileParty, CampaignTime> keyValuePair in villagersBehavior._lootedVillagers)
            {
                if (CampaignTime.Now - keyValuePair.Value >= CampaignTime.Days(10f))
                {
                    list.Add(keyValuePair.Key);
                }
            }
            foreach (MobileParty key in list)
            {
                villagersBehavior._lootedVillagers.Remove(key);
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
            foreach (var deletedVillagerId in obj.What.DeletedLootedVillagersIdsList)
            {
                if (!objectManager.TryGetObjectWithLogging<MobileParty>(deletedVillagerId, out var deletedVillager)) continue;

                GetVillagerBehavior()._lootedVillagers.Remove(deletedVillager);
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
        if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MobilePartyId, out var mobileParty)) return;

        // Update interacted villagers locally on all clients
        GameThread.Run(() =>
        {
            var villagersBehavior = GetVillagerBehavior();
            if (villagersBehavior._interactedVillagers.ContainsKey(mobileParty))
            {
                villagersBehavior._interactedVillagers.Remove(mobileParty);
            }
        });
    }

    private void Handle_NetworkAddToLootedVillagers(MessagePayload<NetworkAddToLootedVillagers> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.VillagersPartyId, out var villagerParty)) return;

            GetVillagerBehavior()._lootedVillagers.Add(villagerParty, obj.What.CampaignTime);
        });
    }

    private VillagerCampaignBehavior GetVillagerBehavior()
    {
        return Campaign.Current.GetCampaignBehavior<VillagerCampaignBehavior>();
    }
}
