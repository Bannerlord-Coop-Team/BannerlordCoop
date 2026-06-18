using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Kingdoms.Messages.Collections;
using GameInterface.Services.ObjectManager;
using GameInterface.Utils.LocalEvents;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace GameInterface.Services.Kingdoms.Handlers;

/// <summary>
/// Handles server-owned Kingdom collection mutations and applies them on clients.
/// </summary>
public class KingdomCollectionHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<KingdomCollectionHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    public KingdomCollectionHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;

        messageBroker.Subscribe<ArmyListUpdated>(Handle);
        messageBroker.Subscribe<NetworkUpdateArmyList>(Handle);
        messageBroker.Subscribe<ArmyListRemoved>(Handle);
        messageBroker.Subscribe<NetworkRemoveArmyList>(Handle);

        messageBroker.Subscribe<ClanListUpdated>(Handle);
        messageBroker.Subscribe<NetworkUpdateClanList>(Handle);
        messageBroker.Subscribe<ClanListRemoved>(Handle);
        messageBroker.Subscribe<NetworkRemoveClanList>(Handle);

        messageBroker.Subscribe<FiefsCacheUpdated>(Handle);
        messageBroker.Subscribe<NetworkUpdateFiefsCache>(Handle);
        messageBroker.Subscribe<FiefsCacheRemoved>(Handle);
        messageBroker.Subscribe<NetworkRemoveFiefsCache>(Handle);

        messageBroker.Subscribe<HeroesCacheUpdated>(Handle);
        messageBroker.Subscribe<NetworkUpdateHeroesCache>(Handle);
        messageBroker.Subscribe<HeroesCacheRemoved>(Handle);
        messageBroker.Subscribe<NetworkRemoveHeroesCache>(Handle);

        messageBroker.Subscribe<AliveLordsCacheUpdated>(Handle);
        messageBroker.Subscribe<NetworkUpdateAliveLordsCache>(Handle);
        messageBroker.Subscribe<AliveLordsCacheRemoved>(Handle);
        messageBroker.Subscribe<NetworkRemoveAliveLordsCache>(Handle);

        messageBroker.Subscribe<DeadLordsCacheUpdated>(Handle);
        messageBroker.Subscribe<NetworkUpdateDeadLordsCache>(Handle);
        messageBroker.Subscribe<DeadLordsCacheRemoved>(Handle);
        messageBroker.Subscribe<NetworkRemoveDeadLordsCache>(Handle);

        messageBroker.Subscribe<SettlementsCacheUpdated>(Handle);
        messageBroker.Subscribe<NetworkUpdateSettlementsCache>(Handle);
        messageBroker.Subscribe<SettlementsCacheRemoved>(Handle);
        messageBroker.Subscribe<NetworkRemoveSettlementsCache>(Handle);

        messageBroker.Subscribe<VillagesCacheUpdated>(Handle);
        messageBroker.Subscribe<NetworkUpdateVillagesCache>(Handle);
        messageBroker.Subscribe<VillagesCacheRemoved>(Handle);
        messageBroker.Subscribe<NetworkRemoveVillagesCache>(Handle);

        messageBroker.Subscribe<WarPartyComponentsCacheUpdated>(Handle);
        messageBroker.Subscribe<NetworkUpdateWarPartyComponentsCache>(Handle);
        messageBroker.Subscribe<WarPartyComponentsCacheRemoved>(Handle);
        messageBroker.Subscribe<NetworkRemoveWarPartyComponentsCache>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ArmyListUpdated>(Handle);
        messageBroker.Unsubscribe<NetworkUpdateArmyList>(Handle);
        messageBroker.Unsubscribe<ArmyListRemoved>(Handle);
        messageBroker.Unsubscribe<NetworkRemoveArmyList>(Handle);

        messageBroker.Unsubscribe<ClanListUpdated>(Handle);
        messageBroker.Unsubscribe<NetworkUpdateClanList>(Handle);
        messageBroker.Unsubscribe<ClanListRemoved>(Handle);
        messageBroker.Unsubscribe<NetworkRemoveClanList>(Handle);

        messageBroker.Unsubscribe<FiefsCacheUpdated>(Handle);
        messageBroker.Unsubscribe<NetworkUpdateFiefsCache>(Handle);
        messageBroker.Unsubscribe<FiefsCacheRemoved>(Handle);
        messageBroker.Unsubscribe<NetworkRemoveFiefsCache>(Handle);

        messageBroker.Unsubscribe<HeroesCacheUpdated>(Handle);
        messageBroker.Unsubscribe<NetworkUpdateHeroesCache>(Handle);
        messageBroker.Unsubscribe<HeroesCacheRemoved>(Handle);
        messageBroker.Unsubscribe<NetworkRemoveHeroesCache>(Handle);

        messageBroker.Unsubscribe<AliveLordsCacheUpdated>(Handle);
        messageBroker.Unsubscribe<NetworkUpdateAliveLordsCache>(Handle);
        messageBroker.Unsubscribe<AliveLordsCacheRemoved>(Handle);
        messageBroker.Unsubscribe<NetworkRemoveAliveLordsCache>(Handle);

        messageBroker.Unsubscribe<DeadLordsCacheUpdated>(Handle);
        messageBroker.Unsubscribe<NetworkUpdateDeadLordsCache>(Handle);
        messageBroker.Unsubscribe<DeadLordsCacheRemoved>(Handle);
        messageBroker.Unsubscribe<NetworkRemoveDeadLordsCache>(Handle);

        messageBroker.Unsubscribe<SettlementsCacheUpdated>(Handle);
        messageBroker.Unsubscribe<NetworkUpdateSettlementsCache>(Handle);
        messageBroker.Unsubscribe<SettlementsCacheRemoved>(Handle);
        messageBroker.Unsubscribe<NetworkRemoveSettlementsCache>(Handle);

        messageBroker.Unsubscribe<VillagesCacheUpdated>(Handle);
        messageBroker.Unsubscribe<NetworkUpdateVillagesCache>(Handle);
        messageBroker.Unsubscribe<VillagesCacheRemoved>(Handle);
        messageBroker.Unsubscribe<NetworkRemoveVillagesCache>(Handle);

        messageBroker.Unsubscribe<WarPartyComponentsCacheUpdated>(Handle);
        messageBroker.Unsubscribe<NetworkUpdateWarPartyComponentsCache>(Handle);
        messageBroker.Unsubscribe<WarPartyComponentsCacheRemoved>(Handle);
        messageBroker.Unsubscribe<NetworkRemoveWarPartyComponentsCache>(Handle);
    }

    private void Handle(MessagePayload<ArmyListUpdated> payload)
    {
        SendNetworkMessage(payload.What, (kingdomId, valueId) => new NetworkUpdateArmyList(kingdomId, valueId));
    }

    private void Handle(MessagePayload<NetworkUpdateArmyList> payload)
    {
        HandleNetworkChange<Army>(payload.What.KingdomId, payload.What.ValueId, ApplyArmyListUpdate);
    }

    private void Handle(MessagePayload<ArmyListRemoved> payload)
    {
        SendNetworkMessage(payload.What, (kingdomId, valueId) => new NetworkRemoveArmyList(kingdomId, valueId));
    }

    private void Handle(MessagePayload<NetworkRemoveArmyList> payload)
    {
        HandleNetworkChange<Army>(payload.What.KingdomId, payload.What.ValueId, ApplyArmyListRemove);
    }

    private void Handle(MessagePayload<ClanListUpdated> payload)
    {
        SendNetworkMessage(payload.What, (kingdomId, valueId) => new NetworkUpdateClanList(kingdomId, valueId));
    }

    private void Handle(MessagePayload<NetworkUpdateClanList> payload)
    {
        HandleNetworkChange<Clan>(payload.What.KingdomId, payload.What.ValueId, ApplyClanListUpdate);
    }

    private void Handle(MessagePayload<ClanListRemoved> payload)
    {
        SendNetworkMessage(payload.What, (kingdomId, valueId) => new NetworkRemoveClanList(kingdomId, valueId));
    }

    private void Handle(MessagePayload<NetworkRemoveClanList> payload)
    {
        HandleNetworkChange<Clan>(payload.What.KingdomId, payload.What.ValueId, ApplyClanListRemove);
    }

    private void Handle(MessagePayload<FiefsCacheUpdated> payload)
    {
        SendNetworkMessage(payload.What, (kingdomId, valueId) => new NetworkUpdateFiefsCache(kingdomId, valueId));
    }

    private void Handle(MessagePayload<NetworkUpdateFiefsCache> payload)
    {
        HandleNetworkChange<Town>(payload.What.KingdomId, payload.What.ValueId, ApplyFiefsCacheUpdate);
    }

    private void Handle(MessagePayload<FiefsCacheRemoved> payload)
    {
        SendNetworkMessage(payload.What, (kingdomId, valueId) => new NetworkRemoveFiefsCache(kingdomId, valueId));
    }

    private void Handle(MessagePayload<NetworkRemoveFiefsCache> payload)
    {
        HandleNetworkChange<Town>(payload.What.KingdomId, payload.What.ValueId, ApplyFiefsCacheRemove);
    }

    private void Handle(MessagePayload<HeroesCacheUpdated> payload)
    {
        SendNetworkMessage(payload.What, (kingdomId, valueId) => new NetworkUpdateHeroesCache(kingdomId, valueId));
    }

    private void Handle(MessagePayload<NetworkUpdateHeroesCache> payload)
    {
        HandleNetworkChange<Hero>(payload.What.KingdomId, payload.What.ValueId, ApplyHeroesCacheUpdate);
    }

    private void Handle(MessagePayload<HeroesCacheRemoved> payload)
    {
        SendNetworkMessage(payload.What, (kingdomId, valueId) => new NetworkRemoveHeroesCache(kingdomId, valueId));
    }

    private void Handle(MessagePayload<NetworkRemoveHeroesCache> payload)
    {
        HandleNetworkChange<Hero>(payload.What.KingdomId, payload.What.ValueId, ApplyHeroesCacheRemove);
    }

    private void Handle(MessagePayload<AliveLordsCacheUpdated> payload)
    {
        SendNetworkMessage(payload.What, (kingdomId, valueId) => new NetworkUpdateAliveLordsCache(kingdomId, valueId));
    }

    private void Handle(MessagePayload<NetworkUpdateAliveLordsCache> payload)
    {
        HandleNetworkChange<Hero>(payload.What.KingdomId, payload.What.ValueId, ApplyAliveLordsCacheUpdate);
    }

    private void Handle(MessagePayload<AliveLordsCacheRemoved> payload)
    {
        SendNetworkMessage(payload.What, (kingdomId, valueId) => new NetworkRemoveAliveLordsCache(kingdomId, valueId));
    }

    private void Handle(MessagePayload<NetworkRemoveAliveLordsCache> payload)
    {
        HandleNetworkChange<Hero>(payload.What.KingdomId, payload.What.ValueId, ApplyAliveLordsCacheRemove);
    }

    private void Handle(MessagePayload<DeadLordsCacheUpdated> payload)
    {
        SendNetworkMessage(payload.What, (kingdomId, valueId) => new NetworkUpdateDeadLordsCache(kingdomId, valueId));
    }

    private void Handle(MessagePayload<NetworkUpdateDeadLordsCache> payload)
    {
        HandleNetworkChange<Hero>(payload.What.KingdomId, payload.What.ValueId, ApplyDeadLordsCacheUpdate);
    }

    private void Handle(MessagePayload<DeadLordsCacheRemoved> payload)
    {
        SendNetworkMessage(payload.What, (kingdomId, valueId) => new NetworkRemoveDeadLordsCache(kingdomId, valueId));
    }

    private void Handle(MessagePayload<NetworkRemoveDeadLordsCache> payload)
    {
        HandleNetworkChange<Hero>(payload.What.KingdomId, payload.What.ValueId, ApplyDeadLordsCacheRemove);
    }

    private void Handle(MessagePayload<SettlementsCacheUpdated> payload)
    {
        SendNetworkMessage(payload.What, (kingdomId, valueId) => new NetworkUpdateSettlementsCache(kingdomId, valueId));
    }

    private void Handle(MessagePayload<NetworkUpdateSettlementsCache> payload)
    {
        HandleNetworkChange<Settlement>(payload.What.KingdomId, payload.What.ValueId, ApplySettlementsCacheUpdate);
    }

    private void Handle(MessagePayload<SettlementsCacheRemoved> payload)
    {
        SendNetworkMessage(payload.What, (kingdomId, valueId) => new NetworkRemoveSettlementsCache(kingdomId, valueId));
    }

    private void Handle(MessagePayload<NetworkRemoveSettlementsCache> payload)
    {
        HandleNetworkChange<Settlement>(payload.What.KingdomId, payload.What.ValueId, ApplySettlementsCacheRemove);
    }

    private void Handle(MessagePayload<VillagesCacheUpdated> payload)
    {
        SendNetworkMessage(payload.What, (kingdomId, valueId) => new NetworkUpdateVillagesCache(kingdomId, valueId));
    }

    private void Handle(MessagePayload<NetworkUpdateVillagesCache> payload)
    {
        HandleNetworkChange<Village>(payload.What.KingdomId, payload.What.ValueId, ApplyVillagesCacheUpdate);
    }

    private void Handle(MessagePayload<VillagesCacheRemoved> payload)
    {
        SendNetworkMessage(payload.What, (kingdomId, valueId) => new NetworkRemoveVillagesCache(kingdomId, valueId));
    }

    private void Handle(MessagePayload<NetworkRemoveVillagesCache> payload)
    {
        HandleNetworkChange<Village>(payload.What.KingdomId, payload.What.ValueId, ApplyVillagesCacheRemove);
    }

    private void Handle(MessagePayload<WarPartyComponentsCacheUpdated> payload)
    {
        SendNetworkMessage(payload.What, (kingdomId, valueId) => new NetworkUpdateWarPartyComponentsCache(kingdomId, valueId));
    }

    private void Handle(MessagePayload<NetworkUpdateWarPartyComponentsCache> payload)
    {
        HandleNetworkChange<WarPartyComponent>(payload.What.KingdomId, payload.What.ValueId, ApplyWarPartyComponentsCacheUpdate);
    }

    private void Handle(MessagePayload<WarPartyComponentsCacheRemoved> payload)
    {
        SendNetworkMessage(payload.What, (kingdomId, valueId) => new NetworkRemoveWarPartyComponentsCache(kingdomId, valueId));
    }

    private void Handle(MessagePayload<NetworkRemoveWarPartyComponentsCache> payload)
    {
        HandleNetworkChange<WarPartyComponent>(payload.What.KingdomId, payload.What.ValueId, ApplyWarPartyComponentsCacheRemove);
    }

    public static void ApplyArmyListUpdate(Kingdom kingdom, Army army)
    {
        AddToList(EnsureList(ref kingdom._armies), army);
    }

    public static void ApplyArmyListRemove(Kingdom kingdom, Army army)
    {
        EnsureList(ref kingdom._armies).Remove(army);
    }

    public static void ApplyClanListUpdate(Kingdom kingdom, Clan clan)
    {
        AddToList(EnsureList(ref kingdom._clans), clan);
    }

    public static void ApplyClanListRemove(Kingdom kingdom, Clan clan)
    {
        EnsureList(ref kingdom._clans).Remove(clan);
    }

    public static void ApplyFiefsCacheUpdate(Kingdom kingdom, Town town)
    {
        AddToList(EnsureList(ref kingdom._fiefsCache), town);
    }

    public static void ApplyFiefsCacheRemove(Kingdom kingdom, Town town)
    {
        EnsureList(ref kingdom._fiefsCache).Remove(town);
    }

    public static void ApplyHeroesCacheUpdate(Kingdom kingdom, Hero hero)
    {
        AddToList(EnsureList(ref kingdom._heroesCache), hero);
    }

    public static void ApplyHeroesCacheRemove(Kingdom kingdom, Hero hero)
    {
        EnsureList(ref kingdom._heroesCache).Remove(hero);
    }

    public static void ApplyAliveLordsCacheUpdate(Kingdom kingdom, Hero hero)
    {
        AddToList(EnsureList(ref kingdom._aliveLordsCache), hero);
    }

    public static void ApplyAliveLordsCacheRemove(Kingdom kingdom, Hero hero)
    {
        EnsureList(ref kingdom._aliveLordsCache).Remove(hero);
    }

    public static void ApplyDeadLordsCacheUpdate(Kingdom kingdom, Hero hero)
    {
        AddToList(EnsureList(ref kingdom._deadLordsCache), hero);
    }

    public static void ApplyDeadLordsCacheRemove(Kingdom kingdom, Hero hero)
    {
        EnsureList(ref kingdom._deadLordsCache).Remove(hero);
    }

    public static void ApplySettlementsCacheUpdate(Kingdom kingdom, Settlement settlement)
    {
        AddToList(EnsureList(ref kingdom._settlementsCache), settlement);
    }

    public static void ApplySettlementsCacheRemove(Kingdom kingdom, Settlement settlement)
    {
        EnsureList(ref kingdom._settlementsCache).Remove(settlement);
    }

    public static void ApplyVillagesCacheUpdate(Kingdom kingdom, Village village)
    {
        AddToList(EnsureList(ref kingdom._villagesCache), village);
    }

    public static void ApplyVillagesCacheRemove(Kingdom kingdom, Village village)
    {
        EnsureList(ref kingdom._villagesCache).Remove(village);
    }

    public static void ApplyWarPartyComponentsCacheUpdate(Kingdom kingdom, WarPartyComponent warPartyComponent)
    {
        AddToList(EnsureList(ref kingdom._warPartyComponentsCache), warPartyComponent);
    }

    public static void ApplyWarPartyComponentsCacheRemove(Kingdom kingdom, WarPartyComponent warPartyComponent)
    {
        EnsureList(ref kingdom._warPartyComponentsCache).Remove(warPartyComponent);
    }

    private void SendNetworkMessage<TValue>(
        GenericEvent<Kingdom, TValue> data,
        Func<string, string, ICommand> createMessage)
    {
        if (!objectManager.TryGetIdWithLogging(data.Instance, out var kingdomId)) return;
        if (!objectManager.TryGetIdWithLogging(data.Value, out var valueId)) return;

        network.SendAll(createMessage(kingdomId, valueId));
    }

    private void HandleNetworkChange<TValue>(
        string kingdomId,
        string valueId,
        Action<Kingdom, TValue> apply)
        where TValue : class
    {
        GameThread.RunSafe(() =>
        {
            try
            {
                if (!objectManager.TryGetObjectWithLogging<Kingdom>(kingdomId, out var kingdom)) return;
                if (!objectManager.TryGetObjectWithLogging<TValue>(valueId, out var value)) return;

                using (new AllowedThread())
                {
                    apply(kingdom, value);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(
                    ex,
                    "Failed to apply Kingdom collection change for Kingdom {KingdomId} and value {ValueId}",
                    kingdomId,
                    valueId);
            }
        }, context: nameof(KingdomCollectionHandler));
    }

    private static MBList<T> EnsureList<T>(ref MBList<T> list)
    {
        list ??= new MBList<T>();
        return list;
    }

    private static void AddToList<T>(MBList<T> list, T value)
    {
        if (!list.Contains(value))
        {
            list.Add(value);
        }
    }
}
