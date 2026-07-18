using System;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Party;

internal interface IPrisonerSaleValidator
{
    TroopRoster Validate(TroopRoster requestedRoster, TroopRoster availableRoster);
}

/// <summary>
/// Clamps a requested prisoner sale to the prisoners in the authoritative roster.
/// </summary>
internal class PrisonerSaleValidator : IPrisonerSaleValidator
{
    public TroopRoster Validate(TroopRoster requestedRoster, TroopRoster availableRoster)
    {
        var validatedRoster = new TroopRoster();

        foreach (var requested in requestedRoster.GetTroopRoster())
        {
            var availableIndex = availableRoster.FindIndexOfTroop(requested.Character);
            if (availableIndex < 0)
                continue;

            var available = availableRoster.GetElementCopyAtIndex(availableIndex);
            var requestedWounded = Math.Min(Math.Max(requested.WoundedNumber, 0), Math.Max(requested.Number, 0));
            var requestedHealthy = Math.Max(requested.Number - requestedWounded, 0);
            var availableWounded = Math.Min(Math.Max(available.WoundedNumber, 0), Math.Max(available.Number, 0));
            var availableHealthy = Math.Max(available.Number - availableWounded, 0);
            var woundedToSell = Math.Min(requestedWounded, availableWounded);
            var healthyToSell = Math.Min(requestedHealthy, availableHealthy);
            var totalToSell = healthyToSell + woundedToSell;

            if (totalToSell == 0)
                continue;

            validatedRoster.AddToCounts(
                requested.Character,
                totalToSell,
                false,
                woundedToSell,
                0,
                true);
        }

        return validatedRoster;
    }
}
