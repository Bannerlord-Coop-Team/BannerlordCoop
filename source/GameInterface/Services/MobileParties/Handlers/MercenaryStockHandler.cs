using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Handlers;

internal class MercenaryStockHandler : IHandler
{
    private const string TownBackstreetMenu = "town_backstreet";
    private static readonly ILogger logger = LogManager.GetLogger<MercenaryStockHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public MercenaryStockHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<CampaignReady>(Handle_CampaignReady);
        messageBroker.Subscribe<MercenaryStockChanged>(Handle_MercenaryStockChanged);
        messageBroker.Subscribe<NetworkUpdateMercenaryStock>(Handle_NetworkUpdateMercenaryStock);
        messageBroker.Subscribe<NetworkRequestMercenaryStockSync>(Handle_NetworkRequestMercenaryStockSync);
        messageBroker.Subscribe<NetworkRequestMercenaryStockAudit>(Handle_NetworkRequestMercenaryStockAudit);
        messageBroker.Subscribe<NetworkMercenaryStockAudit>(Handle_NetworkMercenaryStockAudit);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<CampaignReady>(Handle_CampaignReady);
        messageBroker.Unsubscribe<MercenaryStockChanged>(Handle_MercenaryStockChanged);
        messageBroker.Unsubscribe<NetworkUpdateMercenaryStock>(Handle_NetworkUpdateMercenaryStock);
        messageBroker.Unsubscribe<NetworkRequestMercenaryStockSync>(Handle_NetworkRequestMercenaryStockSync);
        messageBroker.Unsubscribe<NetworkRequestMercenaryStockAudit>(Handle_NetworkRequestMercenaryStockAudit);
        messageBroker.Unsubscribe<NetworkMercenaryStockAudit>(Handle_NetworkMercenaryStockAudit);
    }

    internal void Handle_CampaignReady(MessagePayload<CampaignReady> payload)
    {
        if (ModInformation.IsServer) return;

        network.SendAll(new NetworkRequestMercenaryStockSync());
    }

    internal void Handle_MercenaryStockChanged(MessagePayload<MercenaryStockChanged> payload)
    {
        if (ModInformation.IsClient) return;

        if (!objectManager.TryGetIdWithLogging(payload.What.Town, out var townId)) return;

        string troopTypeId = null;
        if (payload.What.TroopType != null && !objectManager.TryGetId(payload.What.TroopType, out troopTypeId)) return;

        network.SendAll(new NetworkUpdateMercenaryStock(
            townId,
            troopTypeId,
            payload.What.Number));
    }

    private void Handle_NetworkUpdateMercenaryStock(MessagePayload<NetworkUpdateMercenaryStock> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Town>(data.TownId, out var town)) return;

            CharacterObject troopType = null;
            if (data.TroopTypeId != null && !objectManager.TryGetObjectWithLogging<CharacterObject>(data.TroopTypeId, out troopType)) return;

            ApplyMercenaryStock(town, troopType, data.Number);
            RefreshCurrentMercenaryMenu(town);
        }, context: nameof(MercenaryStockHandler));
    }

    internal void Handle_NetworkRequestMercenaryStockSync(MessagePayload<NetworkRequestMercenaryStockSync> payload)
    {
        if (ModInformation.IsClient) return;

        var peer = payload.Who as NetPeer;
        if (peer == null) return;

        GameThread.RunSafe(() => SendMercenaryStockSnapshot(peer), context: nameof(MercenaryStockHandler));
    }

    private void Handle_NetworkRequestMercenaryStockAudit(MessagePayload<NetworkRequestMercenaryStockAudit> payload)
    {
        if (ModInformation.IsServer) return;

        var townId = payload.What.TownId;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Town>(townId, out var town)) return;
            if (!TryGetMercenaryStock(town, out var troopType, out var number)) return;
            if (!objectManager.TryGetIdWithLogging(troopType, out var troopTypeId)) return;

            network.SendAll(new NetworkMercenaryStockAudit(townId, troopTypeId, number));
        }, context: nameof(MercenaryStockHandler));
    }

    private void Handle_NetworkMercenaryStockAudit(MessagePayload<NetworkMercenaryStockAudit> payload)
    {
        if (ModInformation.IsClient) return;

        var data = payload.What;
        var peer = payload.Who as NetPeer;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Town>(data.TownId, out var town)) return;
            if (!TryGetMercenaryStock(town, out var serverTroopType, out var serverNumber)) return;
            if (!objectManager.TryGetIdWithLogging(serverTroopType, out var serverTroopTypeId)) return;

            if (serverTroopTypeId != data.TroopTypeId || serverNumber != data.Number)
            {
                logger.Warning(
                    "Mercenary stock mismatch from {Peer} for {TownId}. Server: {ServerTroopTypeId} x{ServerNumber}; Client: {ClientTroopTypeId} x{ClientNumber}",
                    peer,
                    data.TownId,
                    serverTroopTypeId,
                    serverNumber,
                    data.TroopTypeId,
                    data.Number);
                return;
            }

            logger.Information(
                "Mercenary stock matched from {Peer} for {TownId}: {TroopTypeId} x{Number}",
                peer,
                data.TownId,
                data.TroopTypeId,
                data.Number);
        }, context: nameof(MercenaryStockHandler));
    }

    internal static bool TryGetMercenaryStock(Town town, out CharacterObject troopType, out int number)
    {
        troopType = null;
        number = 0;

        if (!IsMercenaryTown(town)) return false;

        var behavior = Campaign.Current?.GetCampaignBehavior<RecruitmentCampaignBehavior>();
        if (behavior == null)
        {
            logger.Warning("Unable to find {Behavior}", nameof(RecruitmentCampaignBehavior));
            return false;
        }

        var mercenaryData = behavior.GetMercenaryData(town);
        troopType = mercenaryData.TroopType;
        number = mercenaryData.Number;

        return troopType != null;
    }

    internal static void ApplyMercenaryStock(Town town, CharacterObject troopType, int number)
    {
        if (!IsMercenaryTown(town)) return;

        var behavior = Campaign.Current?.GetCampaignBehavior<RecruitmentCampaignBehavior>();
        if (behavior == null)
        {
            logger.Warning("Unable to find {Behavior}", nameof(RecruitmentCampaignBehavior));
            return;
        }

        var mercenaryData = behavior.GetMercenaryData(town);
        mercenaryData.ChangeMercenaryType(troopType, number);
    }

    private void SendMercenaryStockSnapshot(NetPeer peer)
    {
        foreach (var town in GetMercenaryTowns())
        {
            if (!TryGetMercenaryStock(town, out var troopType, out var number)) continue;
            if (!objectManager.TryGetIdWithLogging(town, out var townId)) continue;
            if (!objectManager.TryGetIdWithLogging(troopType, out var troopTypeId)) continue;

            network.Send(peer, new NetworkUpdateMercenaryStock(townId, troopTypeId, number));
        }
    }

    private static void RefreshCurrentMercenaryMenu(Town town)
    {
        if (!IsMercenaryTown(town) ||
            !RecruitmentCampaignBehaviorPatch.TryGetCurrentMercenaryTown(out var currentTown) ||
            currentTown != town ||
            Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId != TownBackstreetMenu) return;

        Campaign.Current.CurrentMenuContext.Refresh();
    }

    private static IEnumerable<Town> GetMercenaryTowns()
    {
        var campaign = Campaign.Current;
        if (campaign == null) return Enumerable.Empty<Town>();

        return campaign.CampaignObjectManager.Settlements
            .Where(settlement => IsMercenaryTown(settlement.Town))
            .Select(settlement => settlement.Town);
    }

    internal static bool IsMercenaryTown(Town town)
    {
        return town?.Settlement != null && town.Settlement.IsTown;
    }
}
