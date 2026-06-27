using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Caravans.Interfaces;
using GameInterface.Services.Caravans.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Caravans.Handlers;

internal class CaravansConversationsHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<CaravansConversationsHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ISessionCaravansPlayerDataInterface sessionCaravansPlayerDataInterface;

    public CaravansConversationsHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ISessionCaravansPlayerDataInterface sessionCaravansPlayerDataInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.sessionCaravansPlayerDataInterface = sessionCaravansPlayerDataInterface;
        messageBroker.Subscribe<ChangeCaravanHomeSettlement>(Handle_ChangeCaravanHomeSettlement);
        messageBroker.Subscribe<NetworkChangeCaravanHomeSettlement>(Handle_NetworkChangeCaravanHomeSettlement);

        messageBroker.Subscribe<ToggleProhibitedKingdom>(Handle_ToggleProhibitedKingdom);
        messageBroker.Subscribe<NetworkToggleProhibitedKingdom>(Handle_NetworkToggleProhibitedKingdom);

        messageBroker.Subscribe<ApplyHostileCaravanInteraction>(Handle_ApplyHostileCaravanInteraction);
        messageBroker.Subscribe<NetworkApplyHostileCaravanInteraction>(Handle_NetworkApplyHostileCaravanInteraction);

        messageBroker.Subscribe<SetPlayerCaravanInteraction>(Handle_SetPlayerCaravanInteraction);
        messageBroker.Subscribe<NetworkSetPlayerCaravanInteraction>(Handle_NetworkSetPlayerCaravanInteraction);

        messageBroker.Subscribe<UpdateTradeRumorTakenCaravans>(Handle_UpdateTradeRumorTakenCaravans);
        messageBroker.Subscribe<NetworkUpdateTradeRumorTakenCaravans>(Handle_NetworkUpdateTradeRumorTakenCaravans);

        messageBroker.Subscribe<CaravanLootedLeaveOnConsequence>(Handle_CaravanLootedLeaveOnConsequence);
        messageBroker.Subscribe<NetworkCaravanLootedLeaveOnConsequence>(Handle_NetworkCaravanLootedLeaveOnConsequence);

        messageBroker.Subscribe<CaravanSurrenderLeaveOnConsequence>(Handle_CaravanSurrenderLeaveOnConsequence);
        messageBroker.Subscribe<NetworkCaravanSurrenderLeaveOnConsequence>(Handle_NetworkCaravanSurrenderLeaveOnConsequence);

        messageBroker.Subscribe<CaravanTookPrisonerOnConsequence>(Handle_CaravanTookPrisonerOnConsequence);
        messageBroker.Subscribe<NetworkCaravanTookPrisonerOnConsequence>(Handle_NetworkCaravanTookPrisonerOnConsequence);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ChangeCaravanHomeSettlement>(Handle_ChangeCaravanHomeSettlement);
        messageBroker.Unsubscribe<NetworkChangeCaravanHomeSettlement>(Handle_NetworkChangeCaravanHomeSettlement);

        messageBroker.Unsubscribe<ToggleProhibitedKingdom>(Handle_ToggleProhibitedKingdom);
        messageBroker.Unsubscribe<NetworkToggleProhibitedKingdom>(Handle_NetworkToggleProhibitedKingdom);

        messageBroker.Unsubscribe<ApplyHostileCaravanInteraction>(Handle_ApplyHostileCaravanInteraction);
        messageBroker.Unsubscribe<NetworkApplyHostileCaravanInteraction>(Handle_NetworkApplyHostileCaravanInteraction);

        messageBroker.Unsubscribe<SetPlayerCaravanInteraction>(Handle_SetPlayerCaravanInteraction);
        messageBroker.Unsubscribe<NetworkSetPlayerCaravanInteraction>(Handle_NetworkSetPlayerCaravanInteraction);

        messageBroker.Unsubscribe<UpdateTradeRumorTakenCaravans>(Handle_UpdateTradeRumorTakenCaravans);
        messageBroker.Unsubscribe<NetworkUpdateTradeRumorTakenCaravans>(Handle_NetworkUpdateTradeRumorTakenCaravans);

        messageBroker.Unsubscribe<CaravanLootedLeaveOnConsequence>(Handle_CaravanLootedLeaveOnConsequence);
        messageBroker.Unsubscribe<NetworkCaravanLootedLeaveOnConsequence>(Handle_NetworkCaravanLootedLeaveOnConsequence);

        messageBroker.Unsubscribe<CaravanSurrenderLeaveOnConsequence>(Handle_CaravanSurrenderLeaveOnConsequence);
        messageBroker.Unsubscribe<NetworkCaravanSurrenderLeaveOnConsequence>(Handle_NetworkCaravanSurrenderLeaveOnConsequence);

        messageBroker.Unsubscribe<CaravanTookPrisonerOnConsequence>(Handle_CaravanTookPrisonerOnConsequence);
        messageBroker.Unsubscribe<NetworkCaravanTookPrisonerOnConsequence>(Handle_NetworkCaravanTookPrisonerOnConsequence);
    }

    private void Handle_ChangeCaravanHomeSettlement(MessagePayload<ChangeCaravanHomeSettlement> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.ConversationParty, out var conversationPartyId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.Settlement, out var settlementId)) return;

        var message = new NetworkChangeCaravanHomeSettlement(conversationPartyId, settlementId);
        network.SendAll(message);
    }

    private void Handle_NetworkChangeCaravanHomeSettlement(MessagePayload<NetworkChangeCaravanHomeSettlement> obj)
    {
        GameThread.Run(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.ConversationPartyId, out var conversationParty)) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.What.SettlementId, out var settlement)) return;

            conversationParty.CaravanPartyComponent.ChangeHomeSettlement(settlement);
        });
    }

    private void Handle_ToggleProhibitedKingdom(MessagePayload<ToggleProhibitedKingdom> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.Kingdom, out var kingdomId)) return;

        var message = new NetworkToggleProhibitedKingdom(mainHeroId, kingdomId, obj.What.KingdomAlreadyProhibited);
        network.SendAll(message);
    }

    private void Handle_NetworkToggleProhibitedKingdom(MessagePayload<NetworkToggleProhibitedKingdom> obj)
    {
        // Guard against saving ids that can't be resolved on the server
        if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.MainHeroId, out var _)) return;
        if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.What.KingdomId, out var _)) return;

        if (obj.What.KingdomAlreadyProhibited)
        {
            sessionCaravansPlayerDataInterface.RemoveProhibitedKingdom(obj.What.MainHeroId, obj.What.KingdomId);
        }
        else
        {
            sessionCaravansPlayerDataInterface.AddProhibitedKingdom(obj.What.MainHeroId, obj.What.KingdomId);
        }
    }

    private void Handle_ApplyHostileCaravanInteraction(MessagePayload<ApplyHostileCaravanInteraction> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.MainParty, out var mainPartyId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.ConversationParty, out var conversationPartyId)) return;

        var message = new NetworkApplyHostileCaravanInteraction(mainHeroId, mainPartyId, conversationPartyId);
        network.SendAll(message);
    }

    private void Handle_NetworkApplyHostileCaravanInteraction(MessagePayload<NetworkApplyHostileCaravanInteraction> obj)
    {
        sessionCaravansPlayerDataInterface.SetPlayerInteraction(obj.What.MainHeroId, obj.What.ConversationPartyId, CaravansCampaignBehavior.PlayerInteraction.Hostile);
        GameThread.Run(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.MainHeroId, out var mainHero)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MainPartyId, out var mainParty)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.ConversationPartyId, out var conversationParty)) return;

            BeHostileAction.ApplyEncounterHostileAction(mainParty.Party, conversationParty.Party);
        });
    }

    private void Handle_SetPlayerCaravanInteraction(MessagePayload<SetPlayerCaravanInteraction> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.ConversationParty, out var conversationPartyId)) return;

        var message = new NetworkSetPlayerCaravanInteraction(mainHeroId, conversationPartyId, obj.What.Interaction);
        network.SendAll(message);
    }

    private void Handle_NetworkSetPlayerCaravanInteraction(MessagePayload<NetworkSetPlayerCaravanInteraction> obj)
    {
        // Guard against saving ids that can't be resolved on the server
        if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.MainHeroId, out var _)) return;
        if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.ConversationPartyId, out var _)) return;

        sessionCaravansPlayerDataInterface.SetPlayerInteraction(obj.What.MainHeroId, obj.What.ConversationPartyId, obj.What.Interaction);
    }

    private void Handle_UpdateTradeRumorTakenCaravans(MessagePayload<UpdateTradeRumorTakenCaravans> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;

        var tradeRumorTakenCaravansIds = new Dictionary<string, long>();
        foreach (var tradeRumorTakenCaravan in obj.What.TradeRumorTakenCaravans)
        {
            if (!objectManager.TryGetIdWithLogging(tradeRumorTakenCaravan.Key, out var mobilePartyId)) continue;

            tradeRumorTakenCaravansIds[mobilePartyId] = tradeRumorTakenCaravan.Value._numTicks;
        }

        var message = new NetworkUpdateTradeRumorTakenCaravans(mainHeroId, tradeRumorTakenCaravansIds);
        network.SendAll(message);
    }

    private void Handle_NetworkUpdateTradeRumorTakenCaravans(MessagePayload<NetworkUpdateTradeRumorTakenCaravans> obj)
    {
        if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.MainHeroId, out var _)) return;

        sessionCaravansPlayerDataInterface.UpdateTradeRumorTakenCaravansForPlayer(obj.What.MainHeroId, obj.What.TradeRumorTakenCaravansIds);
    }

    private void Handle_CaravanLootedLeaveOnConsequence(MessagePayload<CaravanLootedLeaveOnConsequence> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.MainParty, out var mainPartyId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.ConversationParty, out var conversationPartyId)) return;

        var message = new NetworkCaravanLootedLeaveOnConsequence(mainHeroId, mainPartyId, conversationPartyId, obj.What.ItemRosterData, obj.What.Amount);
        network.SendAll(message);
    }

    private void Handle_NetworkCaravanLootedLeaveOnConsequence(MessagePayload<NetworkCaravanLootedLeaveOnConsequence> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.MainHeroId, out var mainHero)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MainPartyId, out var mainParty)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.ConversationPartyId, out var conversationParty)) return;

            ItemRoster itemRoster = new();
            foreach (var itemRosterElement in obj.What.ItemRosterData)
            {
                itemRoster.Add(itemRosterElement);
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
            GetCaravansBehavior()._lootedCaravans.Add(conversationParty, CampaignTime.Now);
            SkillLevelingManager.OnLoot(mainParty, conversationParty, itemRoster, false);

            // Update _lootedCaravans on all clients
            network.SendAll(new NetworkAddToLootedCaravans(obj.What.ConversationPartyId, CampaignTime.Now));
        });
        sessionCaravansPlayerDataInterface.SetPlayerInteraction(obj.What.MainHeroId, obj.What.ConversationPartyId, CaravansCampaignBehavior.PlayerInteraction.Hostile);
    }

    private void Handle_CaravanSurrenderLeaveOnConsequence(MessagePayload<CaravanSurrenderLeaveOnConsequence> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.MainParty, out var mainPartyId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.ConversationParty, out var conversationPartyId)) return;

        var message = new NetworkCaravanSurrenderLeaveOnConsequence(mainHeroId, mainPartyId, conversationPartyId, obj.What.CaravanHasItems, obj.What.ItemRosterElements);
        network.SendAll(message);
    }

    private void Handle_NetworkCaravanSurrenderLeaveOnConsequence(MessagePayload<NetworkCaravanSurrenderLeaveOnConsequence> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.MainHeroId, out var mainHero)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MainPartyId, out var mainParty)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.ConversationPartyId, out var conversationParty)) return;

            ItemRoster itemRoster = new();
            foreach (var itemRosterElement in obj.What.ItemRosterElements)
            {
                itemRoster.Add(itemRosterElement);
            }

            if (obj.What.CaravanHasItems)
            {
                conversationParty.ItemRoster.Clear();
            }
            int num = MathF.Max(conversationParty.PartyTradeGold, 0);
            if (num > 0)
            {
                GiveGoldAction.ApplyForPartyToCharacter(conversationParty.Party, mainHero, num, false);
            }
            BeHostileAction.ApplyMajorCoercionHostileAction(mainParty.Party, conversationParty.Party);
            GetCaravansBehavior()._lootedCaravans.Add(conversationParty, CampaignTime.Now);
            SkillLevelingManager.OnLoot(mainParty, conversationParty, itemRoster, false);

            // Update _lootedCaravans on all clients
            network.SendAll(new NetworkAddToLootedCaravans(obj.What.ConversationPartyId, CampaignTime.Now));
        });
        sessionCaravansPlayerDataInterface.SetPlayerInteraction(obj.What.MainHeroId, obj.What.ConversationPartyId, CaravansCampaignBehavior.PlayerInteraction.Hostile);
    }

    private void Handle_CaravanTookPrisonerOnConsequence(MessagePayload<CaravanTookPrisonerOnConsequence> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.MainParty, out var mainPartyId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.ConversationParty, out var conversationPartyId)) return;

        var message = new NetworkCaravanTookPrisonerOnConsequence(mainHeroId, mainPartyId, conversationPartyId, obj.What.CaravanHasItems, obj.What.ItemRosterElements);
        network.SendAll(message);
    }

    private void Handle_NetworkCaravanTookPrisonerOnConsequence(MessagePayload<NetworkCaravanTookPrisonerOnConsequence> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.MainHeroId, out var mainHero)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MainPartyId, out var mainParty)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.ConversationPartyId, out var conversationParty)) return;

            ItemRoster itemRoster = new();
            foreach (var itemRosterElement in obj.What.ItemRosterElements)
            {
                itemRoster.Add(itemRosterElement);
            }

            if (obj.What.CaravanHasItems)
            {
                conversationParty.ItemRoster.Clear();
            }
            int num = MathF.Max(conversationParty.PartyTradeGold, 0);
            if (num > 0)
            {
                GiveGoldAction.ApplyForPartyToCharacter(conversationParty.Party, mainHero, num, false);
            }
            BeHostileAction.ApplyEncounterHostileAction(mainParty.Party, conversationParty.Party);
            SkillLevelingManager.OnLoot(mainParty, conversationParty, itemRoster, false);
            DestroyPartyAction.Apply(mainParty.Party, conversationParty);
        });
        sessionCaravansPlayerDataInterface.SetPlayerInteraction(obj.What.MainHeroId, obj.What.ConversationPartyId, CaravansCampaignBehavior.PlayerInteraction.Hostile);
    }

    private CaravansCampaignBehavior GetCaravansBehavior()
    {
        return Campaign.Current.GetCampaignBehavior<CaravansCampaignBehavior>();
    }
}
