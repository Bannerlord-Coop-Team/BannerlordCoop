using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Inventory.Data;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Data;
using GameInterface.Services.TroopRosters.Interfaces;
using GameInterface.Services.UI.Notifications.Messages;
using SandBox.GauntletUI.Map;
using SandBox.View.Map;
using SandBox.ViewModelCollection.Nameplate.NameplateNotifications.SettlementNotificationTypes;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.UI.Notifications.Handlers;

/// <summary>
/// Forwards the server-only campaign events behind the settlement nameplate popups to clients,
/// which re-raise them for their own nameplate UI. Only the nameplate VM listens to these
/// events on clients, so re-raising them does not touch game state.
/// </summary>
internal class SettlementNotificationHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<SettlementNotificationHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ITroopRosterInterface troopRosterInterface;

    public SettlementNotificationHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ITroopRosterInterface troopRosterInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.troopRosterInterface = troopRosterInterface;

        messageBroker.Subscribe<NotifyCaravanTransaction>(Handle_NotifyCaravanTransaction);
        messageBroker.Subscribe<NetworkNotifyCaravanTransaction>(Handle_NetworkNotifyCaravanTransaction);
        messageBroker.Subscribe<NotifyTroopRecruited>(Handle_NotifyTroopRecruited);
        messageBroker.Subscribe<NetworkNotifyTroopRecruited>(Handle_NetworkNotifyTroopRecruited);
        messageBroker.Subscribe<NotifyPrisonerSold>(Handle_NotifyPrisonerSold);
        messageBroker.Subscribe<NetworkNotifyPrisonerSold>(Handle_NetworkNotifyPrisonerSold);
        messageBroker.Subscribe<NotifyTroopGivenToSettlement>(Handle_NotifyTroopGivenToSettlement);
        messageBroker.Subscribe<NetworkNotifyTroopGivenToSettlement>(Handle_NetworkNotifyTroopGivenToSettlement);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NotifyCaravanTransaction>(Handle_NotifyCaravanTransaction);
        messageBroker.Unsubscribe<NetworkNotifyCaravanTransaction>(Handle_NetworkNotifyCaravanTransaction);
        messageBroker.Unsubscribe<NotifyTroopRecruited>(Handle_NotifyTroopRecruited);
        messageBroker.Unsubscribe<NetworkNotifyTroopRecruited>(Handle_NetworkNotifyTroopRecruited);
        messageBroker.Unsubscribe<NotifyPrisonerSold>(Handle_NotifyPrisonerSold);
        messageBroker.Unsubscribe<NetworkNotifyPrisonerSold>(Handle_NetworkNotifyPrisonerSold);
        messageBroker.Unsubscribe<NotifyTroopGivenToSettlement>(Handle_NotifyTroopGivenToSettlement);
        messageBroker.Unsubscribe<NetworkNotifyTroopGivenToSettlement>(Handle_NetworkNotifyTroopGivenToSettlement);
    }

    private void Handle_NotifyCaravanTransaction(MessagePayload<NotifyCaravanTransaction> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(data.CaravanParty, out var caravanPartyId)) return;
            if (!objectManager.TryGetIdWithLogging(data.Town, out var townId)) return;

            var items = new List<(ItemObjectData, int)>();
            foreach (var (element, count) in data.Items)
            {
                if (!objectManager.TryGetIdWithLogging(element.Item, out var itemObjectId)) continue;

                string itemModifierId = null;
                if (element.ItemModifier != null && !objectManager.TryGetIdWithLogging(element.ItemModifier, out itemModifierId)) continue;

                items.Add((new ItemObjectData(itemObjectId, itemModifierId, element.ItemModifier == null), count));
            }

            network.SendAll(new NetworkNotifyCaravanTransaction(caravanPartyId, townId, items.ToArray()));
        });
    }

    private void Handle_NetworkNotifyCaravanTransaction(MessagePayload<NetworkNotifyCaravanTransaction> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.CaravanPartyId, out var caravanParty)) return;
            if (!objectManager.TryGetObjectWithLogging<SettlementComponent>(data.TownId, out var settlementComponent)) return;
            if (settlementComponent is not Town town) return;

            var items = new List<(EquipmentElement, int)>();
            foreach (var (itemData, count) in data.Items)
            {
                if (!objectManager.TryGetObjectWithLogging<ItemObject>(itemData.ItemObjectId, out var itemObject)) continue;

                ItemModifier itemModifier = null;
                if (!itemData.ItemModifierNull && !objectManager.TryGetObjectWithLogging(itemData.ItemModifierId, out itemModifier)) continue;

                items.Add((new EquipmentElement(itemObject, itemModifier), count));
            }

            using (new AllowedThread())
            {
                CampaignEventDispatcher.Instance.OnCaravanTransactionCompleted(caravanParty, town, items);
            }
        });
    }

    private void Handle_NotifyTroopRecruited(MessagePayload<NotifyTroopRecruited> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(data.RecruiterHero, out var recruiterHeroId)) return;
            if (!objectManager.TryGetIdWithLogging(data.Settlement, out var settlementId)) return;
            if (!objectManager.TryGetIdWithLogging(data.Troop, out var troopId)) return;

            string troopSourceHeroId = null;
            if (data.TroopSource != null && !objectManager.TryGetIdWithLogging(data.TroopSource, out troopSourceHeroId)) return;

            network.SendAll(new NetworkNotifyTroopRecruited(recruiterHeroId, settlementId, troopSourceHeroId, troopId, data.Amount));
        });
    }

    private void Handle_NetworkNotifyTroopRecruited(MessagePayload<NetworkNotifyTroopRecruited> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.RecruiterHeroId, out var recruiterHero)) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(data.SettlementId, out var settlement)) return;
            if (!objectManager.TryGetObjectWithLogging<CharacterObject>(data.TroopId, out var troop)) return;

            Hero troopSource = null;
            if (data.TroopSourceHeroId != null && !objectManager.TryGetObjectWithLogging(data.TroopSourceHeroId, out troopSource)) return;

            using (new AllowedThread())
            {
                GetSettlementNotifications(settlement)?.OnTroopRecruited(recruiterHero, settlement, troopSource, troop, data.Amount);
            }
        });
    }

    private void Handle_NotifyPrisonerSold(MessagePayload<NotifyPrisonerSold> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(data.SellerParty, out var sellerPartyId)) return;
            if (!objectManager.TryGetIdWithLogging(data.BuyerParty, out var buyerPartyId)) return;

            network.SendAll(new NetworkNotifyPrisonerSold(sellerPartyId, buyerPartyId, troopRosterInterface.PackTroopRosterData(data.Prisoners)));
        });
    }

    private void Handle_NetworkNotifyPrisonerSold(MessagePayload<NetworkNotifyPrisonerSold> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<PartyBase>(data.SellerPartyId, out var sellerParty)) return;
            if (!objectManager.TryGetObjectWithLogging<PartyBase>(data.BuyerPartyId, out var buyerParty)) return;

            using (new AllowedThread())
            {
                CampaignEventDispatcher.Instance.OnPrisonerSold(sellerParty, buyerParty, BuildLocalRoster(data.Prisoners));
            }
        });
    }

    private void Handle_NotifyTroopGivenToSettlement(MessagePayload<NotifyTroopGivenToSettlement> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(data.GiverHero, out var giverHeroId)) return;
            if (!objectManager.TryGetIdWithLogging(data.Settlement, out var settlementId)) return;

            network.SendAll(new NetworkNotifyTroopGivenToSettlement(giverHeroId, settlementId, troopRosterInterface.PackTroopRosterData(data.Troops)));
        });
    }

    private void Handle_NetworkNotifyTroopGivenToSettlement(MessagePayload<NetworkNotifyTroopGivenToSettlement> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.GiverHeroId, out var giverHero)) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(data.SettlementId, out var settlement)) return;

            using (new AllowedThread())
            {
                CampaignEventDispatcher.Instance.OnTroopGivenToSettlement(giverHero, settlement, BuildLocalRoster(data.Troops));
            }
        });
    }

    /// <summary>
    /// Builds an unmanaged roster for the re-raised event; the nameplate popups only read
    /// counts from it. Must be called on an AllowedThread so creation skips registration.
    /// </summary>
    private TroopRoster BuildLocalRoster(TroopRosterData data)
    {
        var roster = TroopRoster.CreateDummyTroopRoster();
        foreach (var element in troopRosterInterface.UnpackTroopRosterData(data))
        {
            roster.Add(element);
        }

        return roster;
    }

    private static SettlementNameplateNotificationsVM GetSettlementNotifications(Settlement settlement)
    {
        var nameplatesVM = MapScreen.Instance?.GetMapView<GauntletMapSettlementNameplateView>()?._dataSource;
        var notifications = nameplatesVM?.GetNameplateOfSettlement(settlement)?.SettlementNotifications;

        return notifications?.IsEventsRegistered == true ? notifications : null;
    }
}
