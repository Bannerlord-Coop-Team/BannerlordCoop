using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MobileParties.Interfaces;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Villages.Messages;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GameInterface.Services.Villages.Handlers;

internal class VillagersConversationsHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<VillagersConversationsHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ISessionInteractionsPlayerDataInterface sessionInteractionsPlayerDataInterface;

    public VillagersConversationsHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ISessionInteractionsPlayerDataInterface sessionInteractionsPlayerDataInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.sessionInteractionsPlayerDataInterface = sessionInteractionsPlayerDataInterface;

        messageBroker.Subscribe<SetPlayerVillagersInteraction>(Handle_SetPlayerVillagersInteraction);
        messageBroker.Subscribe<NetworkSetPlayerVillagersInteraction>(Handle_NetworkSetPlayerVillagersInteraction);

        messageBroker.Subscribe<PlayerBoughtFromVillagersOnConsequence>(Handle_PlayerBoughtFromVillagersOnConsequence);
        messageBroker.Subscribe<NetworkPlayerBoughtFromVillagersOnConsequence>(Handle_NetworkPlayerBoughtFromVillagersOnConsequence);

        messageBroker.Subscribe<ApplyHostileVillagersInteraction>(Handle_ApplyHostileVillagersInteraction);
        messageBroker.Subscribe<NetworkApplyHostileVillagersInteraction>(Handle_NetworkApplyHostileVillagersInteraction);

        messageBroker.Subscribe<VillagersTookPrisonerOnConsequence>(Handle_VillagersTookPrisonerOnConsequence);
        messageBroker.Subscribe<NetworkVillagersTookPrisonerOnConsequence>(Handle_NetworkVillagersTookPrisonerOnConsequence);

        messageBroker.Subscribe<VillagersLootedLeaveOnConsequence>(Handle_VillagersLootedLeaveOnConsequence);
        messageBroker.Subscribe<NetworkVillagersLootedLeaveOnConsequence>(Handle_NetworkVillagersLootedLeaveOnConsequence);

        messageBroker.Subscribe<VillagersSurrenderLeaveOnConsequence>(Handle_VillagersSurrenderLeaveOnConsequence);
        messageBroker.Subscribe<NetworkVillagersSurrenderLeaveOnConsequence>(Handle_NetworkVillagersSurrenderLeaveOnConsequence);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<SetPlayerVillagersInteraction>(Handle_SetPlayerVillagersInteraction);
        messageBroker.Unsubscribe<NetworkSetPlayerVillagersInteraction>(Handle_NetworkSetPlayerVillagersInteraction);

        messageBroker.Unsubscribe<PlayerBoughtFromVillagersOnConsequence>(Handle_PlayerBoughtFromVillagersOnConsequence);
        messageBroker.Unsubscribe<NetworkPlayerBoughtFromVillagersOnConsequence>(Handle_NetworkPlayerBoughtFromVillagersOnConsequence);

        messageBroker.Unsubscribe<ApplyHostileVillagersInteraction>(Handle_ApplyHostileVillagersInteraction);
        messageBroker.Unsubscribe<NetworkApplyHostileVillagersInteraction>(Handle_NetworkApplyHostileVillagersInteraction);

        messageBroker.Unsubscribe<VillagersTookPrisonerOnConsequence>(Handle_VillagersTookPrisonerOnConsequence);
        messageBroker.Unsubscribe<NetworkVillagersTookPrisonerOnConsequence>(Handle_NetworkVillagersTookPrisonerOnConsequence);

        messageBroker.Unsubscribe<VillagersLootedLeaveOnConsequence>(Handle_VillagersLootedLeaveOnConsequence);
        messageBroker.Unsubscribe<NetworkVillagersLootedLeaveOnConsequence>(Handle_NetworkVillagersLootedLeaveOnConsequence);

        messageBroker.Unsubscribe<VillagersSurrenderLeaveOnConsequence>(Handle_VillagersSurrenderLeaveOnConsequence);
        messageBroker.Unsubscribe<NetworkVillagersSurrenderLeaveOnConsequence>(Handle_NetworkVillagersSurrenderLeaveOnConsequence);
    }

    private void Handle_SetPlayerVillagersInteraction(MessagePayload<SetPlayerVillagersInteraction> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.ConversationParty, out var conversationPartyId)) return;

        var message = new NetworkSetPlayerVillagersInteraction(mainHeroId, conversationPartyId, obj.What.Interaction);
        network.SendAll(message);
    }

    private void Handle_NetworkSetPlayerVillagersInteraction(MessagePayload<NetworkSetPlayerVillagersInteraction> obj)
    {
        // Guard against saving ids that can't be resolved on the server
        if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.MainHeroId, out var _)) return;
        if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.ConversationPartyId, out var _)) return;

        sessionInteractionsPlayerDataInterface.SetPlayerVillagersInteraction(obj.What.MainHeroId, obj.What.ConversationPartyId, obj.What.Interaction);
    }

    private void Handle_PlayerBoughtFromVillagersOnConsequence(MessagePayload<PlayerBoughtFromVillagersOnConsequence> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.MainParty, out var mainPartyId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.ConversationParty, out var conversationPartyId)) return;

        var message = new NetworkPlayerBoughtFromVillagersOnConsequence(mainHeroId, mainPartyId, conversationPartyId);
        network.SendAll(message);
    }

    private void Handle_NetworkPlayerBoughtFromVillagersOnConsequence(MessagePayload<NetworkPlayerBoughtFromVillagersOnConsequence> obj)
    {
        if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.MainHeroId, out var mainHero)) return;
        if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MainPartyId, out var mainParty)) return;
        if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.ConversationPartyId, out var conversationParty)) return;

        for (int i = conversationParty.ItemRoster.Count - 1; i >= 0; i--)
        {
            ItemRosterElement elementCopyAtIndex = conversationParty.ItemRoster.GetElementCopyAtIndex(i);
            if (elementCopyAtIndex.EquipmentElement.Item.ItemCategory != DefaultItemCategories.PackAnimal)
            {
                int itemPrice = conversationParty.HomeSettlement.Village.GetItemPrice(elementCopyAtIndex.EquipmentElement, mainParty, true);
                int elementNumber = conversationParty.ItemRoster.GetElementNumber(i);
                int num = itemPrice * elementNumber;
                if (elementNumber > 0)
                {
                    GiveGoldAction.ApplyBetweenCharacters(mainHero, null, num, false);
                    conversationParty.PartyTradeGold += num;
                    mainParty.ItemRoster.AddToCounts(conversationParty.ItemRoster.GetElementCopyAtIndex(i).EquipmentElement, elementNumber);
                    conversationParty.ItemRoster.AddToCounts(conversationParty.ItemRoster.GetElementCopyAtIndex(i).EquipmentElement, -1 * elementNumber);
                }
            }
        }
    }

    private void Handle_ApplyHostileVillagersInteraction(MessagePayload<ApplyHostileVillagersInteraction> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.MainParty, out var mainPartyId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.ConversationParty, out var conversationPartyId)) return;

        var message = new NetworkApplyHostileVillagersInteraction(mainHeroId, mainPartyId, conversationPartyId);
        network.SendAll(message);
    }

    private void Handle_NetworkApplyHostileVillagersInteraction(MessagePayload<NetworkApplyHostileVillagersInteraction> obj)
    {
        sessionInteractionsPlayerDataInterface.SetPlayerVillagersInteraction(obj.What.MainHeroId, obj.What.ConversationPartyId, VillagerCampaignBehavior.PlayerInteraction.Hostile);
        GameThread.Run(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.MainHeroId, out var mainHero)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MainPartyId, out var mainParty)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.ConversationPartyId, out var conversationParty)) return;

            BeHostileAction.ApplyEncounterHostileAction(mainParty.Party, conversationParty.Party);
        });
    }

    private void Handle_VillagersTookPrisonerOnConsequence(MessagePayload<VillagersTookPrisonerOnConsequence> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.MainParty, out var mainPartyId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.ConversationParty, out var conversationPartyId)) return;

        var message = new NetworkVillagersTookPrisonerOnConsequence(mainHeroId, mainPartyId, conversationPartyId, obj.What.ItemRosterElements);
        network.SendAll(message);
    }

    private void Handle_NetworkVillagersTookPrisonerOnConsequence(MessagePayload<NetworkVillagersTookPrisonerOnConsequence> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.MainHeroId, out var mainHero)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MainPartyId, out var mainParty)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.ConversationPartyId, out var conversationParty)) return;

            ItemRoster itemRoster = new();
            if (obj.What.ItemRosterElements != null) // Empty array transferred as null object, guard against NRE if empty
            {
                foreach (var itemRosterElement in obj.What.ItemRosterElements)
                {
                    itemRoster.Add(itemRosterElement);
                }
            }

            if (itemRoster.Count > 0)
            {
                conversationParty.ItemRoster.Clear();
            }
            int partyTradeGold = conversationParty.PartyTradeGold;
            if (partyTradeGold > 0)
            {
                GiveGoldAction.ApplyForPartyToCharacter(conversationParty.Party, mainHero, partyTradeGold, false);
            }

            sessionInteractionsPlayerDataInterface.SetPlayerVillagersInteraction(obj.What.MainHeroId, obj.What.ConversationPartyId, VillagerCampaignBehavior.PlayerInteraction.Hostile);
            BeHostileAction.ApplyEncounterHostileAction(mainParty.Party, conversationParty.Party);
            SkillLevelingManager.OnLoot(mainParty, conversationParty, itemRoster, false);
            DestroyPartyAction.Apply(mainParty.Party, conversationParty);
        });
    }

    private void Handle_VillagersLootedLeaveOnConsequence(MessagePayload<VillagersLootedLeaveOnConsequence> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.MainParty, out var mainPartyId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.ConversationParty, out var conversationPartyId)) return;

        var message = new NetworkVillagersLootedLeaveOnConsequence(mainHeroId, mainPartyId, conversationPartyId, obj.What.ItemRosterData, obj.What.Amount);
        network.SendAll(message);
    }

    private void Handle_NetworkVillagersLootedLeaveOnConsequence(MessagePayload<NetworkVillagersLootedLeaveOnConsequence> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetVillagerBehavior(out var villagerBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.MainHeroId, out var mainHero)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MainPartyId, out var mainParty)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.ConversationPartyId, out var conversationParty)) return;

            ItemRoster itemRoster = new();
            if (obj.What.ItemRosterData != null) // Empty array transferred as null object, guard against NRE if empty
            {
                foreach (var itemRosterElement in obj.What.ItemRosterData)
                {
                    itemRoster.Add(itemRosterElement);
                }
            }

            GiveGoldAction.ApplyForPartyToCharacter(conversationParty.Party, mainHero, obj.What.Amount, false);
            if (!itemRoster.IsEmpty<ItemRosterElement>())
            {
                for (int i = itemRoster.Count - 1; i >= 0; i--)
                {
                    ItemRosterElement itemRosterElement = itemRoster[i];
                    GiveItemAction.ApplyForParties(conversationParty.Party, mainParty.Party, itemRosterElement);
                }
            }
            BeHostileAction.ApplyMinorCoercionHostileAction(mainParty.Party, conversationParty.Party);
            villagerBehavior._lootedVillagers.Add(conversationParty, CampaignTime.Now);
            SkillLevelingManager.OnLoot(mainParty, conversationParty, itemRoster, false);

            sessionInteractionsPlayerDataInterface.SetPlayerVillagersInteraction(obj.What.MainHeroId, obj.What.ConversationPartyId, VillagerCampaignBehavior.PlayerInteraction.Hostile);

            // Update _lootedVillagers on all clients
            network.SendAll(new NetworkAddToLootedVillagers(obj.What.ConversationPartyId, CampaignTime.Now));
        });
    }

    private void Handle_VillagersSurrenderLeaveOnConsequence(MessagePayload<VillagersSurrenderLeaveOnConsequence> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.MainParty, out var mainPartyId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.ConversationParty, out var conversationPartyId)) return;

        var message = new NetworkVillagersSurrenderLeaveOnConsequence(mainHeroId, mainPartyId, conversationPartyId, obj.What.ItemRosterElements);
        network.SendAll(message);
    }

    private void Handle_NetworkVillagersSurrenderLeaveOnConsequence(MessagePayload<NetworkVillagersSurrenderLeaveOnConsequence> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetVillagerBehavior(out var villagerBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.MainHeroId, out var mainHero)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MainPartyId, out var mainParty)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.ConversationPartyId, out var conversationParty)) return;

            ItemRoster itemRoster = new();
            if (obj.What.ItemRosterElements != null) // Empty array transferred as null object, guard against NRE if empty
            {
                foreach (var itemRosterElement in obj.What.ItemRosterElements)
                {
                    itemRoster.Add(itemRosterElement);
                }
            }

            if (itemRoster.Count > 0)
            {
                conversationParty.ItemRoster.Clear();
            }
            int partyTradeGold = conversationParty.PartyTradeGold;
            if (partyTradeGold > 0)
            {
                GiveGoldAction.ApplyForPartyToCharacter(conversationParty.Party, mainHero, partyTradeGold, false);
            }
            BeHostileAction.ApplyMajorCoercionHostileAction(mainParty.Party, conversationParty.Party);
            villagerBehavior._lootedVillagers.Add(conversationParty, CampaignTime.Now);
            SkillLevelingManager.OnLoot(mainParty, conversationParty, itemRoster, false);

            sessionInteractionsPlayerDataInterface.SetPlayerVillagersInteraction(obj.What.MainHeroId, obj.What.ConversationPartyId, VillagerCampaignBehavior.PlayerInteraction.Hostile);

            // Update _lootedVillagers on all clients
            network.SendAll(new NetworkAddToLootedVillagers(obj.What.ConversationPartyId, CampaignTime.Now));
        });
    }

    private bool TryGetVillagerBehavior(out VillagerCampaignBehavior villagerBehavior)
    {
        villagerBehavior = Campaign.Current?.GetCampaignBehavior<VillagerCampaignBehavior>();
        if (villagerBehavior != null) return true;

        Logger.Debug("Skipping villager update because VillagerCampaignBehavior is unavailable");
        return false;
    }
}
