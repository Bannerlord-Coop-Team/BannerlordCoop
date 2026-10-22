using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Coop.Core.Server.Services.Settlements;

/// <summary>
/// Validates a party's server-authoritative interaction range for settlement and siege entry.
/// </summary>
public interface ISettlementEncounterDistanceValidator
{
    /// <summary>
    /// Returns whether the party may interact with the settlement and supplies a rejection reason otherwise.
    /// </summary>
    bool TryValidate(
        MobileParty party,
        Settlement settlement,
        out string rejectionReason);
}

/// <inheritdoc cref="ISettlementEncounterDistanceValidator"/>
internal class SettlementEncounterDistanceValidator : ISettlementEncounterDistanceValidator
{
    private const float PositionSyncDriftSlack = 0.5f;

    public bool TryValidate(
        MobileParty party,
        Settlement settlement,
        out string rejectionReason)
    {
        rejectionReason = null;

        if (party.CurrentSettlement == settlement)
            return true;

        var encounterModel = Campaign.Current?.Models?.EncounterModel;
        if (encounterModel == null)
        {
            rejectionReason = "the server could not validate your distance to the settlement";
            return false;
        }

        bool usePort = party.IsTargetingPort && settlement.HasPort;
        float maximumDistance = usePort && settlement.SiegeEvent?.IsBlockadeActive == true
            ? encounterModel.NeededMaximumDistanceForEncounteringBlockade
            : settlement.IsTown
                ? encounterModel.NeededMaximumDistanceForEncounteringTown
                : encounterModel.NeededMaximumDistanceForEncounteringVillage;
        if (IsWithinInteractionDistance(
            party.Position,
            settlement.Position,
            settlement.GatePosition,
            settlement.PortPosition,
            usePort,
            maximumDistance + PositionSyncDriftSlack))
            return true;

        // Vanilla PlayerEncounter.DoEnd enters the settlement right after its assault is won, while
        // the winner still stands in the siege camp slot the server rolled for it.
        if (settlement.LastAttackerParty == party &&
            party.MapFaction != null &&
            party.MapFaction == settlement.MapFaction &&
            settlement.Town != null &&
            (IsInSiegeCamp(party.Position, settlement.Town.BesiegerCampPositions1, PositionSyncDriftSlack) ||
             IsInSiegeCamp(party.Position, settlement.Town.BesiegerCampPositions2, PositionSyncDriftSlack)))
            return true;

        rejectionReason = "your party is too far from the settlement";
        return false;
    }

    internal static bool IsWithinInteractionDistance(
        CampaignVec2 partyPosition,
        CampaignVec2 settlementPosition,
        CampaignVec2 gatePosition,
        CampaignVec2 portPosition,
        bool usePort,
        float maximumDistance)
    {
        if (usePort)
            return partyPosition.Distance(portPosition) <= maximumDistance;

        // Land movement may converge on either the settlement's center or its visual gate.
        return partyPosition.Distance(gatePosition) <= maximumDistance ||
               partyPosition.Distance(settlementPosition) <= maximumDistance;
    }

    /// <summary>
    /// Returns whether the position lies within slack of a camp slot, the area
    /// BesiegerCamp.GetSiegeCampPartyPosition rolls from (origin + [0,1] * side + [0,1] * forward).
    /// </summary>
    internal static bool IsInSiegeCamp(
        CampaignVec2 partyPosition,
        MatrixFrame[] campFrames,
        float slack)
    {
        if (campFrames == null)
            return false;

        Vec2 position = partyPosition.ToVec2();
        foreach (var frame in campFrames)
        {
            Vec2 side = frame.rotation.s.AsVec2;
            Vec2 forward = frame.rotation.f.AsVec2;
            float determinant = side.x * forward.y - side.y * forward.x;
            if (MathF.Abs(determinant) < 1e-6f)
                continue;

            Vec2 offset = position - frame.origin.AsVec2;
            float sideAmount = MBMath.ClampFloat((offset.x * forward.y - offset.y * forward.x) / determinant, 0f, 1f);
            float forwardAmount = MBMath.ClampFloat((side.x * offset.y - side.y * offset.x) / determinant, 0f, 1f);
            Vec2 closest = frame.origin.AsVec2 + side * sideAmount + forward * forwardAmount;
            if (position.Distance(closest) <= slack)
                return true;
        }

        return false;
    }
}
