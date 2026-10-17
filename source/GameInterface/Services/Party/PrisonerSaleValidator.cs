using System;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Party;

internal interface IPrisonerSaleValidator
{
    TroopRoster Validate(TroopRoster requestedRoster, TroopRoster availableRoster, bool preserveTroopXp = false);
}

/// <summary>
/// Clamps a requested prisoner sale to the prisoners in the authoritative roster.
/// </summary>
internal class PrisonerSaleValidator : IPrisonerSaleValidator
{
    public TroopRoster Validate(TroopRoster requestedRoster, TroopRoster availableRoster, bool preserveTroopXp = false)
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
            var validatedIndex = validatedRoster.FindIndexOfTroop(requested.Character);
            if (validatedIndex >= 0)
            {
                // Earlier entries may have already used part of this character's available stack.
                var alreadyValidated = validatedRoster.GetElementCopyAtIndex(validatedIndex);
                availableWounded -= alreadyValidated.WoundedNumber;
                availableHealthy -= alreadyValidated.Number - alreadyValidated.WoundedNumber;
            }

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
                preserveTroopXp && validatedIndex < 0 ? available.Xp : 0,
                true);
        }

        return validatedRoster;
    }
}
