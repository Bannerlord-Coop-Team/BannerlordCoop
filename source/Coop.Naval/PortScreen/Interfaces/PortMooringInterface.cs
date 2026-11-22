using Common.Network;
using Coop.Naval.CoopSession.Interfaces;
using Coop.Naval.PortScreen.Messages;
using GameInterface.Services;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.Naval.PortScreen.Interfaces;

/// <summary>
/// [Server] Moors player ships at town ports. The server's Settlement.ShipStash holds every player's moored ships with their
/// owner clans in NavalPlayerData, and only the owner's client is sent its ship, so vanilla's port screen only lists that player's ships.
/// </summary>
internal interface IPortMooringInterface : IGameAbstraction
{
    /// <summary>Moors a ship of the player's party at the port they're in. Returns false if it can't be moored.</summary>
    bool TryMoorShip(Hero playerHero, MobileParty playerParty, Settlement settlement, Ship ship);

    /// <summary>Gives a ship the player's clan moored at this port back to their party. Returns false if it can't be.</summary>
    bool TryRetrieveShip(Hero playerHero, MobileParty playerParty, Settlement settlement, Ship ship);
}

/// <inheritdoc cref="IPortMooringInterface"/>
internal class PortMooringInterface : IPortMooringInterface
{
    private readonly ISessionNavalPlayerDataInterface sessionNavalPlayerDataInterface;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly INetwork network;

    public PortMooringInterface(
        ISessionNavalPlayerDataInterface sessionNavalPlayerDataInterface,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        INetwork network)
    {
        this.sessionNavalPlayerDataInterface = sessionNavalPlayerDataInterface;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.network = network;
    }

    public bool TryMoorShip(Hero playerHero, MobileParty playerParty, Settlement settlement, Ship ship)
    {
        if (ship.Owner != playerParty.Party || !CanUsePort(playerHero, playerParty, settlement)) return false;
        if (!objectManager.TryGetIdWithLogging(settlement, out var settlementId)) return false;
        if (!objectManager.TryGetIdWithLogging(playerHero.Clan, out var clanId)) return false;

        // The owner list has to line up with the stash before vanilla appends the ship
        sessionNavalPlayerDataInterface.RecordMooring(settlementId, settlement.ShipStash.Count, clanId);
        ChangeShipOwnerAction.ApplyByStashingShipIntoSettlement(settlement, ship);

        SendToOwner(playerHero, settlementId, ship, isMoored: true);
        return true;
    }

    public bool TryRetrieveShip(Hero playerHero, MobileParty playerParty, Settlement settlement, Ship ship)
    {
        if (!CanUsePort(playerHero, playerParty, settlement)) return false;
        if (!objectManager.TryGetIdWithLogging(settlement, out var settlementId)) return false;
        if (!objectManager.TryGetIdWithLogging(playerHero.Clan, out var clanId)) return false;

        var index = settlement.ShipStash.IndexOf(ship);
        if (index < 0) return false;

        if (!sessionNavalPlayerDataInterface.TryReleaseMooring(settlementId, index, settlement.ShipStash.Count, clanId)) return false;

        ChangeShipOwnerAction.ApplyByUnstashingShipFromSettlement(playerParty.Party, settlement, ship);

        SendToOwner(playerHero, settlementId, ship, isMoored: false);
        return true;
    }

    // A moored ship is only reachable from inside its port, and vanilla never lets a hostile town's port be entered
    private static bool CanUsePort(Hero playerHero, MobileParty playerParty, Settlement settlement)
    {
        if (playerParty.CurrentSettlement != settlement || !settlement.HasPort) return false;

        return !FactionManager.IsAtWarAgainstFaction(settlement.MapFaction, playerHero.MapFaction);
    }

    // Only the owner's client keeps the ship in its stash
    private void SendToOwner(Hero playerHero, string settlementId, Ship ship, bool isMoored)
    {
        if (!playerManager.TryGetPlayer(playerHero, out var player)) return;
        if (!playerManager.TryGetPeer(player.ControllerId, out var peer)) return;
        if (!objectManager.TryGetIdWithLogging(ship, out var shipId)) return;

        network.Send(peer, new NetworkShipMooringChanged(settlementId, shipId, isMoored));
    }
}
