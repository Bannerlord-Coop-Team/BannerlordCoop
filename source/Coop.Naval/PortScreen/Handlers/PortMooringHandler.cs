using Common;
using Common.Logging;
using Common.Messaging;
using Coop.Naval.PortScreen.Messages;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.NavalDLC.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.Naval.PortScreen.Handlers;

/// <summary>
/// [Client] Keeps this client's port stashes to its own clan's moored ships.
/// </summary>
internal class PortMooringHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<PortMooringHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    // Set on join until the stashes are filtered
    private Dictionary<string, List<string>> joinedMooredShipOwnerClans;

    public PortMooringHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;

        messageBroker.Subscribe<NetworkShipMooringChanged>(Handle_NetworkShipMooringChanged);
        messageBroker.Subscribe<InitializeClientMooredShips>(Handle_InitializeClientMooredShips);
        messageBroker.Subscribe<PlayerHeroChanged>(Handle_PlayerHeroChanged);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkShipMooringChanged>(Handle_NetworkShipMooringChanged);
        messageBroker.Unsubscribe<InitializeClientMooredShips>(Handle_InitializeClientMooredShips);
        messageBroker.Unsubscribe<PlayerHeroChanged>(Handle_PlayerHeroChanged);
    }

    private void Handle_NetworkShipMooringChanged(MessagePayload<NetworkShipMooringChanged> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Settlement>(data.SettlementId, out var settlement)) return;
            if (!objectManager.TryGetObjectWithLogging<Ship>(data.ShipId, out var ship)) return;

            if (!data.IsMoored)
            {
                settlement.ShipStash.Remove(ship);
                return;
            }

            if (!settlement.ShipStash.Contains(ship)) settlement.ShipStash.Add(ship);
        });
    }

    private void Handle_InitializeClientMooredShips(MessagePayload<InitializeClientMooredShips> obj)
    {
        // Protobuf skips the constructor, so an empty owner list arrives as null
        joinedMooredShipOwnerClans = obj.What.NavalPlayerData?.MooredShipOwnerClans ?? new Dictionary<string, List<string>>();
    }

    // The joined save's stashes are filtered once, on the hero change that follows the join, because the clan is only known then
    private void Handle_PlayerHeroChanged(MessagePayload<PlayerHeroChanged> obj)
    {
        if (joinedMooredShipOwnerClans == null) return;

        var mooredShipOwnerClans = joinedMooredShipOwnerClans;
        joinedMooredShipOwnerClans = null;
        KeepOnlyClanMooredShips(mooredShipOwnerClans, obj.What.NewHero.Clan.StringId);
    }

    private static void KeepOnlyClanMooredShips(Dictionary<string, List<string>> mooredShipOwnerClans, string playerClanId)
    {
        foreach (var settlement in Campaign.Current.CampaignObjectManager.Settlements)
        {
            var stash = settlement.ShipStash;
            if (stash == null || stash.Count == 0) continue;

            // No record means nobody moored there in co-op, so the ships are from a single player save and belong to no player
            if (!mooredShipOwnerClans.TryGetValue(settlement.StringId, out var ownerClans) || ownerClans == null)
            {
                stash.Clear();
                continue;
            }

            if (ownerClans.Count > stash.Count)
            {
                Logger.Error("{SettlementId} has {OwnerCount} moored ship owners for {ShipCount} moored ships", settlement.StringId, ownerClans.Count, stash.Count);
            }

            // Like the server's owner list, a ship past the end of it has no owner
            for (int i = stash.Count - 1; i >= 0; i--)
            {
                if (i >= ownerClans.Count || ownerClans[i] != playerClanId) stash.RemoveAt(i);
            }
        }
    }
}
