using Common;
using Common.Messaging;
using GameInterface.Services.MapEvents.TroopSupply;
using Missions.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

// The hand-back of adopted hulls: a returning player's launcher fields its ship again, so that hull takes over the ship id
// the battle host simulated, at the adopted hull's frame and condition, once the host released it (a copy cannot become
// simulated in place, and the own hull already sits on the player's team with its formation and crew).
public partial class BattleShipReplicator
{
    internal const float ReclaimRetrySeconds = 2f;
    internal const float HeroBoardingSeconds = 30f;

    // [Returning player] Each adopted ship asked back, by ship id; its own fresh hull stays hidden from peers until the hand-back.
    private readonly Dictionary<Guid, Reclaim> reclaims = new Dictionary<Guid, Reclaim>();
    // Adopted ships the host handed back, so a record still held here spawns under the returning player.
    private readonly HashSet<Guid> handedBackShips = new HashSet<Guid>();
    private readonly HashSet<Guid> unmatchedReclaims = new HashSet<Guid>();
    private readonly Dictionary<BattleSideEnum, List<CoopAgentOrigin>> reserveKeptOnHandBack =
        new Dictionary<BattleSideEnum, List<CoopAgentOrigin>>();
    private Guid heroBoardingShipId;
    private float heroBoardingUntil;
    private long reclaimsSent, reclaimsRefused, handBacksSent, handBacksAccepted, handBacksRejected, duplicatesRemoved, heroesBoarded;

    // Peers see an own hull once its ship is not being asked back.
    private bool IsAnnouncedOwnHull(NetworkShipInfo ship) =>
        IsOwnHull(ship) && !reclaims.Values.Any(reclaim => reclaim.FreshShipId == ship.ShipId);

    // [Game thread, returning player] Null when the record is no adopted hull of this client's or gets no hand-back, so it
    // spawns as a copy; otherwise whether the record is settled.
    private bool? AdvanceReclaim(HeldShip held)
    {
        var data = held.Record;
        if (data.IsNpcParty || !session.IsOwn(data.OriginalOwner) || unmatchedReclaims.Contains(data.ShipId)) return null;

        if (!reclaims.TryGetValue(data.ShipId, out var reclaim))
        {
            var fresh = FindFreshHull(data.CampaignShipId);
            if (fresh == null)
            {
                // The own fleet is fielded by the end of the deployment, so a ship still unmatched then gets no hand-back.
                if (!deployment.IsCommitted || engine.IsDeploymentMode) return false;

                unmatchedReclaims.Add(data.ShipId);
                Logger.Warning("[NavalSync] Adopted hull {ShipId} of this player matches no own hull (campaign ship {CampaignShipId}); the battle host keeps it",
                    data.ShipId, data.CampaignShipId);
                return null;
            }

            reclaim = new Reclaim(data.ShipId, fresh.ShipId, spawnRecordsSent);
            reclaims.Add(data.ShipId, reclaim);
            Logger.Information("[NavalSync] Adopted hull {ShipId} is this player's ship {CampaignShipId}; own hull {FreshShipId} waits for the hand-back",
                data.ShipId, data.CampaignShipId, fresh.ShipId);
        }

        // Copies are held until this client's deployment is over, and the own hull moves only after it too.
        if (!deployment.IsCommitted || engine.IsDeploymentMode) return false;

        if (!Registry.TryGet(reclaim.FreshShipId, out var freshShip) || engine.IsHullRemoved(freshShip.Hull))
        {
            reclaims.Remove(data.ShipId);
            Logger.Warning("[NavalSync] Own hull {FreshShipId} left before adopted hull {ShipId} was handed back; following the host's hull",
                reclaim.FreshShipId, data.ShipId);
            return null;
        }

        // A hull that sank while adopted is not handed back: the duplicate goes and this client follows the host's wreck.
        if (held.Condition?.Condition?.SinkingState > 0 || reclaim.FinalRefusal != null)
        {
            RemoveDuplicate(reclaim, freshShip, reclaim.FinalRefusal ?? "sank while adopted");
            return null;
        }

        var host = session.HostControllerId;
        if (elapsed >= reclaim.NextRequestAt && !string.IsNullOrEmpty(host))
        {
            reclaim.NextRequestAt = elapsed + ReclaimRetrySeconds;
            reclaim.Phase = ReclaimPhase.Requested;
            network.Send(host, new NetworkShipReclaim(data.ShipId, session.OwnControllerId, session.HostEpoch,
                engine.FreeCrewCapacity(freshShip.Hull)));
            reclaimsSent++;
            Logger.Information("[NavalSync] Asked battle host {Host} for adopted hull {ShipId} back", host, data.ShipId);
        }

        return false;
    }

    private NetworkShipInfo FindFreshHull(string campaignShipId)
    {
        if (string.IsNullOrEmpty(campaignShipId)) return null;

        return Registry.Ships.FirstOrDefault(ship => IsAnnouncedOwnHull(ship) && !ship.FollowsBattleHost
            && engine.CampaignShipIdOf(ship.Hull) == campaignShipId);
    }

    // M8a's park-then-remove takes the duplicate out; its own crew withdraws with it, as vanilla withdraws a removed hull's.
    private void RemoveDuplicate(Reclaim reclaim, NetworkShipInfo fresh, string why)
    {
        reclaims.Remove(reclaim.ShipId);
        stations.ReleaseSeatsOn(fresh.Hull);
        engine.ReleaseHullStations(fresh.Hull);
        MoveDeckAgents(fresh, new[] { fresh.Hull });
        if (reclaim.FreshAnnounced)
            network.SendAll(new NetworkShipRemoved(fresh.ShipId, session.OwnControllerId, ShipRemovalReason.Reclaimed));
        hullAuthority.Remove(new[] { fresh }, withdrawCrew: true);
        Prune(fresh.ShipId);
        duplicatesRemoved++;
        Logger.Information("[NavalSync] Adopted hull {ShipId} is not handed back ({Reason}); removed own duplicate {FreshShipId}",
            reclaim.ShipId, why, fresh.ShipId);
    }

    private void Handle_NetworkShipReclaim(MessagePayload<NetworkShipReclaim> payload)
    {
        var reclaim = payload.What;
        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null) return;
            AcceptReclaim(reclaim);
        }, context: nameof(Handle_NetworkShipReclaim));
    }

    /// <summary>[Game thread, battle host] Hands an adopted hull back to the player who fielded it, or tells it why not.</summary>
    internal void AcceptReclaim(NetworkShipReclaim reclaim)
    {
        if (reclaim == null || string.IsNullOrEmpty(reclaim.ReturningControllerId)) return;

        var reason = RefuseReclaim(reclaim, out var ship, out bool isTransient);
        if (reason != null)
        {
            reclaimsRefused++;
            network.Send(reclaim.ReturningControllerId,
                new NetworkShipReclaimRefused(reclaim.ShipId, session.OwnControllerId, reason, isTransient));
            Logger.Information("[NavalSync] Refused {Controller} adopted hull {ShipId}: {Reason}", reclaim.ReturningControllerId,
                reclaim.ShipId, reason);
            return;
        }

        HandBack(ship, reclaim.ReturningControllerId, reclaim.FreeReserveSlots);
    }

    // Why the host keeps the hull, or null; a transient reason is asked again later. A repeat after the hand-back reads not_adopted.
    private string RefuseReclaim(NetworkShipReclaim reclaim, out NetworkShipInfo ship, out bool isTransient)
    {
        isTransient = false;
        if (!Registry.TryGet(reclaim.ShipId, out ship)) return "unknown_ship";
        if (ship.IsNpcParty || reclaim.ReturningControllerId != ship.OriginalOwner) return "not_original_owner";
        if (!ship.IsAdopted) return "not_adopted";
        if (!BattleAuthorityMigrator.AppliesHostMigration(resultCommitter, session.IsNaval)) return "battle_resolved";
        if (engine.IsHullRemoved(ship.Hull) || engine.IsSinking(ship.Hull)) return "sunk";

        isTransient = true;
        if (!IsOwnHull(ship)) return "not_simulated_here";
        if (reclaim.HostEpoch != session.HostEpoch) return "stale_epoch";
        if (AuthorityStamp.ValidateMember(reclaim.ReturningControllerId, missionContext.ControllersInMission) != null)
            return "not_in_mission";
        if (hullAuthority.IsReplacing(ship.ShipId)) return "swapping";

        isTransient = false;
        return null;
    }

    // The host's own crew leave the hull's machines, its own queued troops of the player go back up to what the player's hull
    // can take, and the rest stays with the host.
    private void HandBack(NetworkShipInfo ship, string returningControllerId, int freeReserveSlots)
    {
        engine.ReleaseHullStations(ship.Hull);

        var handedBackReserve = new List<TroopReserveEntry>();
        var kept = new List<CoopAgentOrigin>();
        foreach (var origin in engine.TakeHullReserve(ship.Hull).OfType<CoopAgentOrigin>())
        {
            if (origin.MapEventPartyId == ship.MapEventPartyId && handedBackReserve.Count < freeReserveSlots
                && objectManager.TryGetId(origin.Troop, out var characterId))
                handedBackReserve.Add(new TroopReserveEntry(origin.UniqueSeed, characterId, 0, 0, origin.InitialHealth));
            else
                kept.Add(origin);
        }

        if (kept.Count > 0) KeptReserve(engine.SideOf(ship.Hull)).AddRange(kept);

        var handedBack = new NetworkShipHandedBack(ship.ShipId, session.OwnControllerId, returningControllerId, session.HostEpoch,
            NetworkBattleShipSample.FromFrame(engine.GetFrame(ship.Hull)), engine.ReadCondition(ship.Hull), ship.MapEventPartyId,
            handedBackReserve.ToArray());
        network.SendAll(handedBack);
        handBacksSent++;
        ApplyHandBack(ship, returningControllerId);
        Logger.Information("[NavalSync] Handed adopted hull {ShipId} back to {Controller} with {Reserve} queued troop(s); kept {Kept}",
            ship.ShipId, returningControllerId, handedBackReserve.Count, kept.Count);
    }

    private List<CoopAgentOrigin> KeptReserve(BattleSideEnum side)
    {
        if (!reserveKeptOnHandBack.TryGetValue(side, out var kept))
        {
            kept = new List<CoopAgentOrigin>();
            reserveKeptOnHandBack.Add(side, kept);
        }

        return kept;
    }

    public IReadOnlyList<CoopAgentOrigin> TakeReserveKeptOnHandBack(BattleSideEnum side)
    {
        if (!reserveKeptOnHandBack.TryGetValue(side, out var kept) || kept.Count == 0) return Array.Empty<CoopAgentOrigin>();

        reserveKeptOnHandBack.Remove(side);
        return kept;
    }

    private void Handle_NetworkShipHandedBack(MessagePayload<NetworkShipHandedBack> payload)
    {
        var handedBack = payload.What;
        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null) return;
            AcceptHandBack(handedBack);
        }, context: nameof(Handle_NetworkShipHandedBack));
    }

    /// <summary>
    /// [Game thread] The battle host released an adopted hull: every client moves it to the returning player, which takes it
    /// over with its own hull of that ship.
    /// </summary>
    internal void AcceptHandBack(NetworkShipHandedBack handedBack)
    {
        if (handedBack == null || session.IsOwn(handedBack.HostControllerId)) return;

        if (!handedBack.IsValid
            || AuthorityStamp.ValidateMember(handedBack.HostControllerId, missionContext.ControllersInMission) != null
            || hostEpochPolicy.IsStale(handedBack.HostEpoch, session.HostEpoch))
        {
            RejectHandBack(handedBack, "invalid_or_stale");
            return;
        }

        if (session.IsOwn(handedBack.ReturningControllerId))
        {
            CompleteReclaim(handedBack);
            return;
        }

        if (!Registry.TryGet(handedBack.ShipId, out var ship))
        {
            if (!Registry.IsGone(handedBack.ShipId)) handedBackShips.Add(handedBack.ShipId);
            handBacksAccepted++;
            return;
        }

        var rejection = ship.CurrentAuthority != handedBack.HostControllerId ? "not_authority"
            : handedBack.ReturningControllerId != ship.OriginalOwner ? "not_original_owner"
            : !ship.IsAdopted ? "not_adopted"
            : null;
        if (rejection != null)
        {
            RejectHandBack(handedBack, rejection);
            return;
        }

        ApplyHandBack(ship, handedBack.ReturningControllerId);
        handBacksAccepted++;
    }

    private void RejectHandBack(NetworkShipHandedBack handedBack, string reason)
    {
        handBacksRejected++;
        Logger.Warning("[NavalSync] Dropped the hand-back of hull {ShipId} from {Host} to {Controller}: {Reason}", handedBack.ShipId,
            handedBack.HostControllerId, handedBack.ReturningControllerId, reason);
    }

    private void ApplyHandBack(NetworkShipInfo ship, string returningControllerId)
    {
        handedBackShips.Add(ship.ShipId);
        TransferHull(ship, returningControllerId);
    }

    // [Returning player] The host's samples for the ship id stop here: the own hull takes the id, moves to the hull's last frame
    // and takes its condition and the queued troops that came back.
    private void CompleteReclaim(NetworkShipHandedBack handedBack)
    {
        if (!reclaims.TryGetValue(handedBack.ShipId, out var reclaim) || !Registry.TryGet(reclaim.FreshShipId, out var fresh))
        {
            reclaims.Remove(handedBack.ShipId);
            RejectHandBack(handedBack, "no_own_hull_waiting");
            return;
        }

        heldShips.TryGetValue(handedBack.ShipId, out var held);
        if (!session.IsHostController(handedBack.HostControllerId) && held?.Record?.OwnerControllerId != handedBack.HostControllerId)
        {
            RejectHandBack(handedBack, "not_authority");
            return;
        }

        reclaims.Remove(handedBack.ShipId);
        heldShips.Remove(handedBack.ShipId);
        Registry.Remove(fresh.ShipId);
        streams.Remove(fresh.ShipId);
        conditions.Forget(fresh.ShipId);
        if (reclaim.FreshAnnounced)
            network.SendAll(new NetworkShipRemoved(fresh.ShipId, session.OwnControllerId, ShipRemovalReason.Reclaimed));

        var ship = new NetworkShipInfo(handedBack.ShipId, session.OwnControllerId, held?.Record?.MapEventPartyId ?? fresh.MapEventPartyId,
            false, fresh.Hull, fresh.Formation);
        Registry.TryRegister(ship);
        handedBackShips.Add(ship.ShipId);

        // Only the hull's own crew may be aboard when it moves; vanilla's teleport carries them.
        stations.ReleaseSeatsOn(ship.Hull);
        MoveDeckAgents(ship, new[] { ship.Hull });
        engine.TeleportHull(ship.Hull, NetworkBattleShipSample.ToFrame(handedBack.Frame));
        engine.ApplyCondition(ship.Hull, handedBack.Condition);
        int queued = engine.AddHullReserve(ship.Hull, ReturnedReserve(handedBack, ship));

        heroBoardingShipId = ship.ShipId;
        heroBoardingUntil = elapsed + HeroBoardingSeconds;
        handBacksAccepted++;
        Logger.Information("[NavalSync] Took back hull {ShipId} from {Host} on own hull {FreshShipId}, {Queued} queued troop(s) back",
            ship.ShipId, handedBack.HostControllerId, fresh.ShipId, queued);
    }

    // The troops come back as the own supplier's origins would be built, so their removal still counts toward its quota.
    private IReadOnlyList<IAgentOriginBase> ReturnedReserve(NetworkShipHandedBack handedBack, NetworkShipInfo ship)
    {
        // An empty array arrives as null, since protobuf skips the constructor.
        var reserve = handedBack.Reserve ?? Array.Empty<TroopReserveEntry>();
        if (reserve.Length == 0) return Array.Empty<IAgentOriginBase>();

        var side = engine.SideOf(ship.Hull);
        var supplier = CoopTroopSupplierRegistry.GetSuppliers(session.InstanceId).FirstOrDefault(candidate => candidate.Side == side);
        objectManager.TryGetObject<MapEventParty>(handedBack.ReservePartyId, out var mapEventParty);
        var origins = new List<IAgentOriginBase>();
        foreach (var entry in reserve)
        {
            if (!objectManager.TryGetObject<CharacterObject>(entry.CharacterId, out var character)) continue;

            origins.Add(new CoopAgentOrigin(character, mapEventParty?.Party, -1, null, new UniqueTroopDescriptor(entry.Seed),
                handedBack.ReservePartyId, supplier, entry.Health));
        }

        return origins;
    }

    private void Handle_NetworkShipReclaimRefused(MessagePayload<NetworkShipReclaimRefused> payload)
    {
        var refused = payload.What;
        GameThread.RunSafe(() => AcceptReclaimRefusal(refused), context: nameof(Handle_NetworkShipReclaimRefused));
    }

    /// <summary>[Game thread, returning player] A transient refusal is asked again; any other ends the reclaim at the next tick.</summary>
    internal void AcceptReclaimRefusal(NetworkShipReclaimRefused refused)
    {
        if (refused == null || !reclaims.TryGetValue(refused.ShipId, out var reclaim)) return;

        reclaim.LastRefusal = refused.Reason;
        if (refused.IsTransient)
        {
            reclaim.Phase = ReclaimPhase.Waiting;
            reclaim.NextRequestAt = elapsed + ReclaimRetrySeconds;
        }
        else
        {
            reclaim.Phase = ReclaimPhase.Refused;
            reclaim.FinalRefusal = refused.Reason;
        }

        Logger.Information("[NavalSync] Battle host {Host} refused adopted hull {ShipId}: {Reason}{Transient}", refused.HostControllerId,
            refused.ShipId, refused.Reason, refused.IsTransient ? " (asking again)" : "");
    }

    // The returned hero is spawned where it stood on the host's hull; once that hull is the own one, it boards it if it is off
    // every hull.
    private void TickHeroBoarding()
    {
        if (heroBoardingShipId == Guid.Empty) return;

        if (elapsed > heroBoardingUntil || !Registry.TryGet(heroBoardingShipId, out var ship) || !IsOwnHull(ship))
        {
            heroBoardingShipId = Guid.Empty;
            return;
        }

        var hero = Mission.Current?.MainAgent;
        if (hero == null || !engine.IsAlive(hero)) return;

        heroBoardingShipId = Guid.Empty;
        if (engine.GetSupportHull(hero) != null) return;

        engine.BoardAgent(hero, ship.Hull);
        heroesBoarded++;
        Logger.Information("[NavalSync] Boarded the returned hero onto handed-back hull {ShipId}", ship.ShipId);
    }

    public object InspectHandBack() => new
    {
        reclaimsSent,
        reclaimsRefused,
        handBacksSent,
        handBacksAccepted,
        handBacksRejected,
        duplicatesRemoved,
        heroesBoarded,
        playerHulls = Registry.Ships.Where(ship => !ship.IsNpcParty).Select(ship => new
        {
            shipId = ship.ShipId,
            originalOwner = ship.OriginalOwner,
            authority = ship.CurrentAuthority,
            phase = ship.IsAdopted ? "adopted" : handedBackShips.Contains(ship.ShipId) ? "handed_back" : "with_owner",
        }).ToArray(),
        reclaims = reclaims.Values.Select(reclaim => new
        {
            shipId = reclaim.ShipId,
            freshShipId = reclaim.FreshShipId,
            phase = reclaim.Phase.ToString(),
            lastRefusal = reclaim.LastRefusal,
        }).ToArray(),
    };

    private enum ReclaimPhase
    {
        Matched,
        Requested,
        Waiting,
        Refused,
    }

    /// <summary>An adopted ship this returning client asked back, and its own fresh hull of the same campaign ship.</summary>
    private sealed class Reclaim
    {
        public Reclaim(Guid shipId, Guid freshShipId, bool freshAnnounced)
        {
            ShipId = shipId;
            FreshShipId = freshShipId;
            FreshAnnounced = freshAnnounced;
        }

        public Guid ShipId { get; }
        public Guid FreshShipId { get; }
        /// <summary>Whether peers were told of the fresh hull before it was matched, so its removal goes out too.</summary>
        public bool FreshAnnounced { get; }
        public ReclaimPhase Phase { get; set; }
        public float NextRequestAt { get; set; }
        public string LastRefusal { get; set; }
        public string FinalRefusal { get; set; }
    }
}
