using Common.Logging;
using Common.Network;
using Coop.Naval.PortScreen.Messages;
using GameInterface.CoopSessionData;
using GameInterface.Services;
using GameInterface.Services.NavalDLC;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.Naval.PortScreen.Interfaces;

/// <summary>
/// Moors player ships at town ports. The server's Settlement.ShipStash holds every player's moored ships with their owner
/// clans in NavalPlayerData, and each client's holds only its own, so vanilla's port screen only lists that player's ships.
/// </summary>
internal interface IPortMooringInterface : IGameAbstraction
{
    /// <summary>[Server] Moors a ship of the player's party at the port they're in. Returns false if it can't be moored.</summary>
    bool TryMoorShip(Hero playerHero, MobileParty playerParty, Settlement settlement, Ship ship);

    /// <summary>[Server] Gives a ship the player's clan moored at this port back to their party. Returns false if it can't be.</summary>
    bool TryRetrieveShip(Hero playerHero, MobileParty playerParty, Settlement settlement, Ship ship);

    /// <summary>[Client] Removes the ships other clans moored from this client's stashes, since the joined save has everyone's.</summary>
    void KeepOnlyClanMooredShips(NavalPlayerData navalPlayerData, Clan playerClan);
}

/// <inheritdoc cref="IPortMooringInterface"/>
internal class PortMooringInterface : IPortMooringInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<PortMooringInterface>();

    private readonly ICoopSessionProvider coopSessionProvider;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly INetwork network;

    public PortMooringInterface(
        ICoopSessionProvider coopSessionProvider,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        INetwork network)
    {
        this.coopSessionProvider = coopSessionProvider;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.network = network;
    }

    public bool TryMoorShip(Hero playerHero, MobileParty playerParty, Settlement settlement, Ship ship)
    {
        if (ship.Owner != playerParty.Party || !CanUsePort(playerHero, playerParty, settlement)) return false;

        var ownerClans = GetOwnerClans(settlement);
        ChangeShipOwnerAction.ApplyByStashingShipIntoSettlement(settlement, ship);
        ownerClans.Add(playerHero.Clan.StringId);

        SendToOwner(playerHero, settlement, ship, isMoored: true);
        return true;
    }

    public bool TryRetrieveShip(Hero playerHero, MobileParty playerParty, Settlement settlement, Ship ship)
    {
        if (!CanUsePort(playerHero, playerParty, settlement)) return false;

        var index = settlement.ShipStash.IndexOf(ship);
        if (index < 0) return false;

        var ownerClans = GetOwnerClans(settlement);
        if (ownerClans[index] != playerHero.Clan.StringId) return false;

        ChangeShipOwnerAction.ApplyByUnstashingShipFromSettlement(playerParty.Party, settlement, ship);
        ownerClans.RemoveAt(index);

        SendToOwner(playerHero, settlement, ship, isMoored: false);
        return true;
    }

    public void KeepOnlyClanMooredShips(NavalPlayerData navalPlayerData, Clan playerClan)
    {
        foreach (var settlement in Campaign.Current.CampaignObjectManager.Settlements)
        {
            var stash = settlement.ShipStash;
            if (stash == null || stash.Count == 0) continue;

            // No record means nobody moored there in co-op, so the ships are from a single player save and no player's
            List<string> ownerClans = null;
            navalPlayerData?.MooredShipOwnerClans?.TryGetValue(settlement.StringId, out ownerClans);
            if (ownerClans == null)
            {
                stash.Clear();
                continue;
            }

            // A different count means this stash was already reduced to this player's ships
            if (ownerClans.Count != stash.Count) continue;

            for (int i = stash.Count - 1; i >= 0; i--)
            {
                if (ownerClans[i] != playerClan.StringId) stash.RemoveAt(i);
            }
        }
    }

    // A moored ship is only reachable from inside its port, and vanilla never lets a hostile town's port be entered
    private static bool CanUsePort(Hero playerHero, MobileParty playerParty, Settlement settlement)
    {
        if (playerParty.CurrentSettlement != settlement || !settlement.HasPort) return false;

        return !FactionManager.IsAtWarAgainstFaction(settlement.MapFaction, playerHero.MapFaction);
    }

    // Padded with no owner for ships moored before co-op, which no player can take back
    private List<string> GetOwnerClans(Settlement settlement)
    {
        var mooredShipOwnerClans = coopSessionProvider.CoopSession.NavalPlayerData.MooredShipOwnerClans;
        if (!mooredShipOwnerClans.TryGetValue(settlement.StringId, out var ownerClans))
        {
            ownerClans = new List<string>();
            mooredShipOwnerClans[settlement.StringId] = ownerClans;
        }

        while (ownerClans.Count < settlement.ShipStash.Count) ownerClans.Add(string.Empty);
        if (ownerClans.Count > settlement.ShipStash.Count)
        {
            Logger.Error("{SettlementId} has {OwnerCount} moored ship owners for {ShipCount} moored ships", settlement.StringId, ownerClans.Count, settlement.ShipStash.Count);
            ownerClans.RemoveRange(settlement.ShipStash.Count, ownerClans.Count - settlement.ShipStash.Count);
        }

        return ownerClans;
    }

    // Only the owner's client keeps the ship in its stash
    private void SendToOwner(Hero playerHero, Settlement settlement, Ship ship, bool isMoored)
    {
        if (!playerManager.TryGetPlayer(playerHero, out var player)) return;
        if (!playerManager.TryGetPeer(player.ControllerId, out var peer)) return;
        if (!objectManager.TryGetIdWithLogging(settlement, out var settlementId)) return;
        if (!objectManager.TryGetIdWithLogging(ship, out var shipId)) return;

        network.Send(peer, new NetworkShipMooringChanged(settlementId, shipId, isMoored));
    }
}
