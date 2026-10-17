using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MobileParties.Messages.Behavior;
using Helpers;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace GameInterface.Services.MobileParties;

/// <summary>
/// Server repair for a land party left standing on a map spot its navigation cannot use, such as
/// the water or settlement ground around a town. Native movement never leaves such a spot: the path
/// search fails and every straight step is refused because it crosses into another navigation
/// island, so the server copy freezes while the client keeps walking.
/// </summary>
internal interface IPartyNavigationRepair
{
    /// <summary>
    /// Moves a free land party standing on an unusable map spot to the closest usable one, clears the
    /// path state computed from the bad spot and force-syncs the new position to every client.
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

    public PartyNavigationRepair(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;
    }

    public bool TryRepair(MobileParty party, out CampaignVec2 previousPosition)
    {
        previousPosition = party.Position;

        // Only a party moving on its own over land: settlement, battle, siege and army members are
        // positioned by those systems, and naval positions follow different navigation rules.
        if (!party.IsActive || party.IsCurrentlyAtSea || party.CurrentSettlement != null ||
            party.Party?.MapEvent != null || party.BesiegerCamp != null || party.AttachedTo != null)
            return false;

        const MobileParty.NavigationType navigation = MobileParty.NavigationType.Default;
        if (NavigationHelper.IsPositionValidForNavigationType(previousPosition, navigation)) return false;

        if (!TryFindUsablePosition(previousPosition, navigation, out var destination))
        {
            Logger.Error(
                "Party {PartyId} stands on an unusable map spot at {Position} and no usable spot was found to move it to",
                party.StringId, previousPosition);
            return false;
        }

        party.Position = destination;
        // Drop the path cached from the bad spot so the next tick computes one from the new spot.
        party._pathMode = false;
        party.PathLastFace = PathFaceRecord.NullFaceRecord;
        party.PathBegin = 0;
        party._lastComputedPathNavigationType = MobileParty.NavigationType.None;

        Logger.Warning(
            "Moved party {PartyId} from an unusable map spot at {Previous} to {Position}",
            party.StringId, previousPosition, destination);

        messageBroker.Publish(this, new PartyBehaviorChangeAttempted(
            party,
            forcePosition: true,
            isCurrentlyAtSea: party.IsCurrentlyAtSea));
        return true;
    }

    /// <summary>
    /// The closest usable navigation face, the way native map-change repair finds one, else the
    /// nearest settlement gate that is usable.
    /// </summary>
    private static bool TryFindUsablePosition(
        CampaignVec2 position,
        MobileParty.NavigationType navigation,
        out CampaignVec2 destination)
    {
        var invalidTerrains = Campaign.Current.Models.PartyNavigationModel.GetInvalidTerrainTypesForNavigationType(navigation);
        destination = NavigationHelper.GetClosestNavMeshFaceCenterPositionForPosition(position, invalidTerrains);
        if (NavigationHelper.IsPositionValidForNavigationType(destination, navigation)) return true;

        var nearestDistance = float.MaxValue;
        var found = false;
        foreach (var settlement in Settlement.All)
        {
            var gate = settlement.GatePosition;
            if (!NavigationHelper.IsPositionValidForNavigationType(gate, navigation)) continue;

            var distance = gate.Distance(position);
            if (distance >= nearestDistance) continue;

            destination = gate;
            nearestDistance = distance;
            found = true;
        }

        return found;
    }
}
