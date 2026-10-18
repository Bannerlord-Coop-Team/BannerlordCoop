using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MobileParties.Handlers;
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
/// Server repair for a land party left standing on a map spot its navigation cannot use, such as
/// the water or settlement ground around a town, or a scrap of land cut off from the rest of the
/// map. Native movement never leaves such a spot: the path search fails and every straight step is
/// refused because it crosses into another navigation island, so the server copy freezes while the
/// client keeps walking.
/// </summary>
internal interface IPartyNavigationRepair
{
    /// <summary>
    /// Moves a free land party (with the parties attached to it when it leads an army) standing on an
    /// unusable map spot to the closest usable one, clears the path state computed from the bad spot
    /// and force-syncs the new positions to every client.
    /// </summary>
    /// <param name="previousPosition">The server position before the repair.</param>
    /// <returns>True when the party was moved.</returns>
    bool TryRepair(MobileParty party, out CampaignVec2 previousPosition);
}

/// <inheritdoc cref="IPartyNavigationRepair"/>
internal class PartyNavigationRepair : IPartyNavigationRepair
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyNavigationRepair>();

    private readonly IMessageBroker messageBroker;

    // Islands holding a usable settlement gate, kept per navigation mesh since the map never changes.
    private IMapScene reachableIslandsScene;
    private HashSet<int> reachableIslands;

    public PartyNavigationRepair(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;
    }

    public bool TryRepair(MobileParty party, out CampaignVec2 previousPosition)
    {
        previousPosition = party.Position;

        // Only a party moving on its own over land: settlement, battle, siege and attached army
        // members are positioned by those systems, and naval positions follow different navigation
        // rules. An army leader is repaired together with its attached parties below.
        if (!party.IsActive || party.IsCurrentlyAtSea || party.CurrentSettlement != null ||
            party.Party?.MapEvent != null || party.BesiegerCamp != null || party.AttachedTo != null)
            return false;

        const MobileParty.NavigationType navigation = MobileParty.NavigationType.Default;
        var islands = GetReachableIslands(navigation);
        // Without a usable gate there is nothing to tell the connected map from a cut off island.
        if (islands.Count == 0) return false;
        if (IsUsable(previousPosition, navigation, islands)) return false;

        if (!TryFindUsablePosition(previousPosition, navigation, islands, out var destination))
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
            attachedParties = MobilePartyBehaviorHandler.ApplyServerForcedPosition(party, destination, party.IsCurrentlyAtSea);
        else
            party.Position = destination;

        // Drop the path cached from the bad spot so the next tick computes one from the new spot.
        ResetPath(party);

        Logger.Warning(
            "Moved party {PartyId} from an unusable map spot at {Previous} to {Position}",
            party.StringId, previousPosition, destination);

        PublishForcedPosition(party);
        if (attachedParties != null)
        {
            foreach (var attachedParty in attachedParties)
            {
                ResetPath(attachedParty);
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

    private static void ResetPath(MobileParty party)
    {
        party._pathMode = false;
        party.PathLastFace = PathFaceRecord.NullFaceRecord;
        party.PathBegin = 0;
        party._lastComputedPathNavigationType = MobileParty.NavigationType.None;
    }

    /// <summary>
    /// A spot is usable when its terrain allows the navigation and its face lies on a navigation
    /// island that also holds a usable settlement gate. Native movement never steps from one island
    /// into another, so valid terrain on a cut off island still leaves the party frozen.
    /// </summary>
    private static bool IsUsable(CampaignVec2 position, MobileParty.NavigationType navigation, HashSet<int> islands)
    {
        if (!NavigationHelper.IsPositionValidForNavigationType(position, navigation)) return false;

        var island = position.Face.FaceIslandIndex;
        return island >= 0 && islands.Contains(island);
    }

    private HashSet<int> GetReachableIslands(MobileParty.NavigationType navigation)
    {
        var scene = Campaign.Current.MapSceneWrapper;
        if (reachableIslands != null && ReferenceEquals(scene, reachableIslandsScene)) return reachableIslands;

        var islands = new HashSet<int>();
        foreach (var settlement in Settlement.All)
        {
            var gate = settlement.GatePosition;
            if (!NavigationHelper.IsPositionValidForNavigationType(gate, navigation)) continue;

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
    /// The closest usable navigation face, the way native map-change repair finds one, else the
    /// nearest usable settlement gate, which by definition lies on a reachable island.
    /// </summary>
    private static bool TryFindUsablePosition(
        CampaignVec2 position,
        MobileParty.NavigationType navigation,
        HashSet<int> islands,
        out CampaignVec2 destination)
    {
        var invalidTerrains = Campaign.Current.Models.PartyNavigationModel.GetInvalidTerrainTypesForNavigationType(navigation);
        destination = NavigationHelper.GetClosestNavMeshFaceCenterPositionForPosition(position, invalidTerrains);
        if (IsUsable(destination, navigation, islands)) return true;

        var nearestDistance = float.MaxValue;
        var found = false;
        foreach (var settlement in Settlement.All)
        {
            var gate = settlement.GatePosition;
            if (!IsUsable(gate, navigation, islands)) continue;

            var distance = gate.Distance(position);
            if (distance >= nearestDistance) continue;

            destination = gate;
            nearestDistance = distance;
            found = true;
        }

        return found;
    }
}
