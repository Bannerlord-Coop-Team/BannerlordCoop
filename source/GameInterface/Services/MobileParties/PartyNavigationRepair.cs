using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using Helpers;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace GameInterface.Services.MobileParties;

/// <summary>
/// Server repair for a land party standing on a map spot native movement can never leave.
/// </summary>
internal interface IPartyNavigationRepair
{
    /// <summary>
    /// Moves the party (and its attached parties) to the closest usable spot and force-syncs it.
    /// </summary>
    bool TryRepair(MobileParty party, out CampaignVec2 previousPosition);

    /// <summary>
    /// Same repair for a movement order, but leaves valid gate free land alone when the target is on that island.
    /// </summary>
    bool TryRepairForOrder(MobileParty party);
}

/// <inheritdoc cref="IPartyNavigationRepair"/>
internal class PartyNavigationRepair : IPartyNavigationRepair
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyNavigationRepair>();

    // Only free land parties are repaired, so every check uses land navigation.
    private const MobileParty.NavigationType Navigation = MobileParty.NavigationType.Default;

    private readonly IMessageBroker messageBroker;

    // Islands holding a usable settlement gate, kept per navigation mesh since the map never changes.
    private IMapScene reachableIslandsScene;
    private HashSet<int> reachableIslands;

    public PartyNavigationRepair(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;
    }

    public bool TryRepair(MobileParty party, out CampaignVec2 previousPosition) =>
        TryRepair(party, forOrder: false, out previousPosition);

    public bool TryRepairForOrder(MobileParty party) =>
        TryRepair(party, forOrder: true, out _);

    private bool TryRepair(MobileParty party, bool forOrder, out CampaignVec2 previousPosition)
    {
        previousPosition = party.Position;

        // Only a party moving on its own over land: settlement, battle, siege and attached army
        // members are positioned by those systems, and naval positions follow different navigation
        // rules. An army leader is repaired together with its attached parties below.
        if (!party.IsActive || party.IsCurrentlyAtSea || party.CurrentSettlement != null ||
            party.Party?.MapEvent != null || party.BesiegerCamp != null || party.AttachedTo != null)
            return false;

        var islands = GetReachableIslands();
        // Without a usable gate there is nothing to tell the connected map from a cut off island.
        if (islands.Count == 0) return false;
        if (IsUsable(previousPosition, islands)) return false;
        // Valid land without a gate is still navigable within its island, so an order only moves
        // the party off it when the order cannot be followed from there.
        if (forOrder && NavigationHelper.IsPositionValidForNavigationType(previousPosition, Navigation) &&
            CanReachOrderTarget(party, previousPosition))
            return false;

        if (!TryFindUsablePosition(previousPosition, islands, out var destination))
        {
            Logger.Error(
                "Party {PartyId} stands on an unusable map spot at {Position} and no usable spot was found to move it to",
                party.StringId, previousPosition);
            return false;
        }

        // An army leader moves the way a forced position update moves it on the server, together
        // with every attached party, so the army is not left split across both spots.
        List<MobileParty> attachedParties = null;
        if (party.Army?.LeaderParty == party)
            attachedParties = party.ApplyForcedPositionWithAttachedParties(destination, party.IsCurrentlyAtSea);
        else
            party.Position = destination;

        // Drop the path cached from the bad spot so the next tick computes one from the new spot.
        party.ResetPath();

        Logger.Warning(
            "Moved party {PartyId} from an unusable map spot at {Previous} to {Position}",
            party.StringId, previousPosition, destination);

        PublishForcedPosition(party);
        if (attachedParties != null)
        {
            foreach (var attachedParty in attachedParties)
            {
                attachedParty.ResetPath();
                PublishForcedPosition(attachedParty);
            }
        }
        return true;
    }

    private void PublishForcedPosition(MobileParty party)
    {
        messageBroker.Publish(this, new PartyBehaviorChangeAttempted(
            party,
            forcePosition: true,
            isCurrentlyAtSea: party.IsCurrentlyAtSea));
    }

    /// <summary>
    /// Valid terrain on a navigation island that holds a usable settlement gate.
    /// </summary>
    private static bool IsUsable(CampaignVec2 position, HashSet<int> islands)
    {
        if (!NavigationHelper.IsPositionValidForNavigationType(position, Navigation)) return false;

        var island = position.Face.FaceIslandIndex;
        return island >= 0 && islands.Contains(island);
    }

    /// <summary>
    /// A hold order or a target on the party's own island, since native movement never crosses islands.
    /// </summary>
    private static bool CanReachOrderTarget(MobileParty party, CampaignVec2 position)
    {
        CampaignVec2 target;
        if (party.PartyMoveMode == MoveModeType.Point)
            target = party.MoveTargetPoint;
        else if (party.MoveTargetParty != null)
            target = party.MoveTargetParty.Position;
        else
            return true;

        if (!NavigationHelper.IsPositionValidForNavigationType(target, Navigation)) return false;
        return target.Face.FaceIslandIndex == position.Face.FaceIslandIndex;
    }

    private HashSet<int> GetReachableIslands()
    {
        var scene = Campaign.Current.MapSceneWrapper;
        if (reachableIslands != null && ReferenceEquals(scene, reachableIslandsScene)) return reachableIslands;

        var islands = new HashSet<int>();
        foreach (var settlement in Settlement.All)
        {
            var gate = settlement.GatePosition;
            if (!NavigationHelper.IsPositionValidForNavigationType(gate, Navigation)) continue;

            var island = gate.Face.FaceIslandIndex;
            if (island >= 0) islands.Add(island);
        }

        // An empty set can just mean the settlements are not loaded yet, so it is not kept.
        if (islands.Count > 0)
        {
            reachableIslandsScene = scene;
            reachableIslands = islands;
        }
        return islands;
    }

    /// <summary>
    /// The closest usable face center, else the nearest usable settlement gate.
    /// </summary>
    private static bool TryFindUsablePosition(CampaignVec2 position, HashSet<int> islands, out CampaignVec2 destination)
    {
        var invalidTerrains = Campaign.Current.Models.PartyNavigationModel.GetInvalidTerrainTypesForNavigationType(Navigation);
        destination = NavigationHelper.GetClosestNavMeshFaceCenterPositionForPosition(position, invalidTerrains);
        if (IsUsable(destination, islands)) return true;

        var nearestDistance = float.MaxValue;
        var found = false;
        foreach (var settlement in Settlement.All)
        {
            var gate = settlement.GatePosition;
            if (!IsUsable(gate, islands)) continue;

            var distance = gate.Distance(position);
            if (distance >= nearestDistance) continue;

            destination = gate;
            nearestDistance = distance;
            found = true;
        }

        return found;
    }
}
