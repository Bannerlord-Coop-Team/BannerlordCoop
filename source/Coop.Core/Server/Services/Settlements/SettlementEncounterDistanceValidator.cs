using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

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
        var targetPosition = usePort ? settlement.PortPosition : settlement.GatePosition;

        if (party.Position.Distance(targetPosition) <= maximumDistance + PositionSyncDriftSlack)
            return true;

        rejectionReason = "your party is too far from the settlement";
        return false;
    }
}
