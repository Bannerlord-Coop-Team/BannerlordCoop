using Common.Messaging;
using GameInterface.CoopSessionData;
using GameInterface.Registry.Auto;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.NavalDLC;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.Rafts.Interfaces;

/// <summary>
/// Server-only: strands a player's party released from captivity at sea and keeps its fleet until it washes ashore or
/// leaves its raft at sea.
/// </summary>
public interface IPlayerRaftFleetInterface : IGameAbstraction
{
    /// <summary>
    /// Takes the fleet away from a party released at sea and puts it on a raft.
    /// </summary>
    void StrandAfterRelease(MobileParty party);

    /// <summary>
    /// Gives a party that washed ashore back the fleet it had when it was stranded.
    /// </summary>
    void ReturnFleet(MobileParty party);

    /// <summary>
    /// Ends a player party's raft state where it is at sea and gives back its fleet unanchored, as the naval scenario staging does.
    /// </summary>
    void LeaveRaftAtSea(MobileParty party);

    /// <summary>
    /// Lands a party stranded on land as a raft or marked at sea where it stands, ends its raft state and gives back its fleet.
    /// Returns false for a party not on land or with nothing to land.
    /// </summary>
    bool PutAshore(MobileParty party);

    /// <summary>
    /// Puts a player's new party on a raft or gives it the fleet cached for that player, like
    /// RaftStateCampaignBehavior.OnPlayerCharacterChanged.
    /// </summary>
    void OnPlayerCharacterChanged(Hero newPlayerHero, MobileParty newMainParty);
}

public class PlayerRaftFleetInterface : IPlayerRaftFleetInterface
{
    private readonly ICoopSessionProvider coopSessionProvider;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly IMessageBroker messageBroker;

    private Dictionary<string, PlayerCachedFleetData> PlayerCachedFleets => coopSessionProvider.CoopSession.NavalPlayerData.PlayerCachedFleets;

    public PlayerRaftFleetInterface(ICoopSessionProvider coopSessionProvider, IObjectManager objectManager, IPlayerManager playerManager, IMessageBroker messageBroker)
    {
        this.coopSessionProvider = coopSessionProvider;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.messageBroker = messageBroker;
    }

    public void StrandAfterRelease(MobileParty party)
    {
        if (!party.IsCurrentlyAtSea || party.IsInRaftState) return;

        // Same as RaftStateCampaignBehavior.player_raft_state_after_prisoner_on_consequence
        if (party.Ships.Count > 0)
        {
            if (!playerManager.TryGetPlayer(party, out var player)) return;

            // Cached before the ships are removed, same as vanilla
            var anchorPosition = party.Anchor.Position;
            var disembarkPosition = party.Anchor.GetLastUsedDisembarkPosition();

            // Vanilla adds to the ships still cached from an earlier stranding instead of replacing them
            var ships = PlayerCachedFleets.TryGetValue(player.HeroId, out var cachedFleet) && cachedFleet.Ships != null
                ? new List<CachedShipData>(cachedFleet.Ships)
                : new List<CachedShipData>();
            for (int i = party.Ships.Count - 1; i >= 0; i--)
            {
                var ship = party.Ships[i];
                ships.Add(CreateShipData(ship));
                ship.Owner = null;

                // The fleet is rebuilt from its cached data, so the ownerless ship is unregistered everywhere
                messageBroker.Publish(this, new InstanceDestroyed<Ship>(ship));
            }

            PlayerCachedFleets[player.HeroId] = new PlayerCachedFleetData(
                ships,
                anchorPosition.X,
                anchorPosition.Y,
                anchorPosition.IsOnLand,
                disembarkPosition.X,
                disembarkPosition.Y,
                disembarkPosition.IsOnLand);
        }

        RaftStateChangeAction.ActivateRaftStateForParty(party);
    }

    public void ReturnFleet(MobileParty party)
    {
        if (!playerManager.TryGetPlayer(party, out var player)) return;
        if (!TryTakeCachedFleet(player.HeroId, party, out var fleet)) return;

        // Same as RaftStateCampaignBehavior.player_raft_state_end_continue_on_consequence
        party.Anchor.SetPosition(new CampaignVec2(new Vec2(fleet.AnchorX, fleet.AnchorY), fleet.AnchorIsOnLand));
        party.Anchor.SetLastUsedDisembarkPosition(new CampaignVec2(new Vec2(fleet.DisembarkX, fleet.DisembarkY), fleet.DisembarkIsOnLand));
    }

    public void LeaveRaftAtSea(MobileParty party)
    {
        if (!party.IsInRaftState) return;

        EndRaftState(party);
        ReturnFleet(party);
        // ReturnFleet restores the coast anchor cached at the stranding, but a party at sea sails with its fleet, unanchored.
        party.Anchor.ResetPosition();
    }

    public bool PutAshore(MobileParty party)
    {
        // Only a party already on land, so a raft still at sea can't skip washing ashore
        if (!party.Position.IsOnLand) return false;
        // Nothing to land, neither a raft nor marked at sea
        if (!party.IsInRaftState && !party.IsCurrentlyAtSea) return false;

        if (party.IsTransitionInProgress)
            party.CancelNavigationTransition();

        party.DisembarkToPosition(party.Position);
        party.SetMoveModeHold();
        party.ResetNavigationToHold();

        if (party.IsInRaftState)
            EndRaftState(party);

        // Unlike LeaveRaftAtSea, the fleet keeps the coast anchor ReturnFleet restores, like a landing
        ReturnFleet(party);
        return true;
    }

    // RaftStateChangeAction.DeactivateRaftStateForParty without the short-term behavior and AI rethink for a party its client
    // moves; EnableAi restores vanilla's non-raft value of the synced _isDisabled, which the raft's DisableAi set.
    private static void EndRaftState(MobileParty party)
    {
        party.IsInRaftState = false;
        party.Ai.EnableAi();
        CampaignEventDispatcher.Instance.OnMobilePartyRaftStateChanged(party);
    }

    public void OnPlayerCharacterChanged(Hero newPlayerHero, MobileParty newMainParty)
    {
        // Same as RaftStateCampaignBehavior.OnPlayerCharacterChanged, which drops the cached anchor instead of using it
        if (newMainParty.IsCurrentlyAtSea && !newMainParty.IsInRaftState && !newMainParty.HasNavalNavigationCapability && newMainParty.IsActive)
        {
            RaftStateChangeAction.ActivateRaftStateForParty(newMainParty);
            return;
        }

        // A rejoin also arrives here, so a party still on its raft keeps the fleet for ReturnFleet to give back with its anchor on landing
        if (newMainParty.IsInRaftState) return;

        if (!objectManager.TryGetIdWithLogging(newPlayerHero, out var playerHeroId)) return;

        TryTakeCachedFleet(playerHeroId, newMainParty, out _);
    }

    // Same as RaftStateCampaignBehavior.GiveCachedShipsToMainParty
    private bool TryTakeCachedFleet(string playerHeroId, MobileParty party, out PlayerCachedFleetData fleet)
    {
        if (!PlayerCachedFleets.TryGetValue(playerHeroId, out fleet)) return false;

        PlayerCachedFleets.Remove(playerHeroId);

        foreach (var shipData in fleet.Ships ?? new List<CachedShipData>())
        {
            RestoreShip(party, shipData);
        }

        return true;
    }

    private static CachedShipData CreateShipData(Ship ship)
    {
        var slotPieceIds = new Dictionary<string, string>();
        foreach (var slot in ship.ShipHull.AvailableSlots)
        {
            var piece = ship.GetPieceAtSlot(slot.Key);
            if (piece != null)
            {
                slotPieceIds[slot.Key] = piece.StringId;
            }
        }

        var unlockedPieceIds = ship.UnlockedUpgradePieces?.Select(piece => piece.StringId).ToList() ?? new List<string>();

        return new CachedShipData(
            ship.ShipHull.StringId,
            ship._name?.Value,
            ship.HitPoints,
            ship.SailHitPoints,
            ship.Figurehead?.StringId,
            ship.CustomSailPatternId,
            ship.RandomValue,
            slotPieceIds,
            unlockedPieceIds);
    }

    private void RestoreShip(MobileParty party, CachedShipData data)
    {
        if (!objectManager.TryGetObjectWithLogging<ShipHull>(data.HullId, out var hull)) return;

        var ship = new Ship(hull);
        ChangeShipOwnerAction.ApplyByTransferring(party.Party, ship);

        if (data.Name != null)
        {
            ship.SetName(new TextObject(data.Name));
        }
        if (data.FigureheadId != null && objectManager.TryGetObjectWithLogging<Figurehead>(data.FigureheadId, out var figurehead))
        {
            ship.ChangeFigurehead(figurehead);
        }
        ship.CustomSailPatternId = data.CustomSailPatternId ?? string.Empty;
        ship.RandomValue = data.RandomValue;

        var unlockedPieces = new List<ShipUpgradePiece>();
        foreach (var pieceId in data.UnlockedPieceIds ?? new List<string>())
        {
            if (objectManager.TryGetObjectWithLogging<ShipUpgradePiece>(pieceId, out var piece))
            {
                unlockedPieces.Add(piece);
            }
        }

        // Equipping unlocks each piece for a player party, then the piece that was fitted goes back on
        var slotPieceIds = data.SlotPieceIds ?? new Dictionary<string, string>();
        foreach (var slot in hull.AvailableSlots)
        {
            foreach (var piece in unlockedPieces)
            {
                if (piece.DoesPieceMatchSlot(slot.Value))
                {
                    ship.EquipUpgradePiece(slot.Key, piece);
                }
            }

            ShipUpgradePiece fittedPiece = null;
            if (slotPieceIds.TryGetValue(slot.Key, out var fittedPieceId))
            {
                objectManager.TryGetObjectWithLogging(fittedPieceId, out fittedPiece);
            }
            ship.EquipUpgradePiece(slot.Key, fittedPiece);
        }

        // Set after the pieces because they change the max hit points the setters clamp to
        ship.HitPoints = data.HitPoints;
        ship.SailHitPoints = data.SailHitPoints;
    }
}
