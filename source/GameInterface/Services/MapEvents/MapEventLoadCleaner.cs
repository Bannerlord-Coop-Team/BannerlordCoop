using Serilog;
using GameInterface.Services.MobileParties.Extensions;
using Helpers;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MapEvents;

public interface IMapEventLoadCleaner
{
    void FinalizePlayerMapEvents();
}

internal sealed class MapEventLoadCleaner : IMapEventLoadCleaner
{
    private readonly ILogger logger;

    public MapEventLoadCleaner(ILogger logger)
    {
        this.logger = logger;
    }

    public void FinalizePlayerMapEvents()
    {
        if (Campaign.Current?.MapEventManager == null)
            return;

        var loadedPlayerMapEvents = Campaign.Current.MapEventManager.MapEvents
            .Where(mapEvent => !mapEvent.IsFinalized && mapEvent.ContainsPlayerParty())
            .ToArray();

        foreach (var mapEvent in loadedPlayerMapEvents)
        {
            var involvedMobileParties = mapEvent.InvolvedParties
                .Where(party => party.IsMobile && party.MobileParty.IsActive)
                .Select(party => party.MobileParty)
                .ToArray();
            var playerLedArmies = involvedMobileParties
                .Select(mobileParty => mobileParty.Army)
                .Where(army => army != null && army.LeaderParty.IsPlayerParty())
                .Distinct()
                .ToArray();
            var releasedArmyParties = playerLedArmies
                .SelectMany(army => army.Parties)
                .Where(mobileParty => mobileParty.IsActive && !mobileParty.IsPlayerParty())
                .Distinct()
                .ToArray();

            logger.Information(
                "Finalizing loaded player map event {MapEventId} with {PartyCount} involved parties",
                mapEvent.StringId,
                mapEvent.InvolvedParties.Count());
            mapEvent.FinalizeEvent();

            foreach (var army in playerLedArmies)
            {
                logger.Information(
                    "Dispersing loaded player-led army {ArmyName} released from map event {MapEventId}",
                    army.Name,
                    mapEvent.StringId);
                DisbandArmyAction.ApplyByUnknownReason(army);
            }

            foreach (var mobileParty in involvedMobileParties.Concat(releasedArmyParties).Distinct())
            {
                if (!mobileParty.IsActive || mobileParty.IsPlayerParty())
                    continue;

                if (releasedArmyParties.Contains(mobileParty) &&
                    TrySetReleasedPartySettlementObjective(mobileParty, out var settlement))
                {
                    logger.Information(
                        "Sending released army party {PartyId} to {SettlementId} after finalizing map event {MapEventId}",
                        mobileParty.StringId,
                        settlement.StringId,
                        mapEvent.StringId);
                    continue;
                }

                mobileParty.ResetNavigationToHold();
            }
        }
    }

    private static bool TrySetReleasedPartySettlementObjective(
        MobileParty mobileParty,
        out Settlement settlement)
    {
        var navigationType = mobileParty.IsCurrentlyAtSea
            ? MobileParty.NavigationType.Naval
            : MobileParty.NavigationType.Default;

        settlement = FindDestination(mobileParty, navigationType, requireFriendly: true) ??
            FindDestination(mobileParty, navigationType, requireFriendly: false);
        if (settlement == null)
            return false;

        SetPartyAiAction.GetActionForVisitingSettlement(
            mobileParty,
            settlement,
            navigationType,
            isFromPort: false,
            isTargetingPort: mobileParty.IsCurrentlyAtSea);
        return true;
    }

    private static Settlement FindDestination(
        MobileParty mobileParty,
        MobileParty.NavigationType navigationType,
        bool requireFriendly)
    {
        bool IsEligible(Settlement candidate) =>
            IsEligibleDestination(mobileParty, candidate, requireFriendly);

        if (IsEligible(mobileParty.HomeSettlement))
            return mobileParty.HomeSettlement;

        return SettlementHelper.FindNearestSettlementToMobileParty(
                mobileParty,
                navigationType,
                IsEligible) ??
            SettlementHelper.FindNearestSettlementToPoint(mobileParty.Position, IsEligible);
    }

    private static bool IsEligibleDestination(
        MobileParty mobileParty,
        Settlement settlement,
        bool requireFriendly)
    {
        if (settlement == null ||
            (!settlement.IsFortification && !settlement.IsVillage) ||
            settlement.IsUnderSiege ||
            settlement.IsUnderRaid ||
            (mobileParty.IsCurrentlyAtSea && !settlement.HasPort))
        {
            return false;
        }

        return !requireFriendly ||
            mobileParty.MapFaction == null ||
            settlement.MapFaction == null ||
            !FactionManager.IsAtWarAgainstFaction(mobileParty.MapFaction, settlement.MapFaction);
    }
}
