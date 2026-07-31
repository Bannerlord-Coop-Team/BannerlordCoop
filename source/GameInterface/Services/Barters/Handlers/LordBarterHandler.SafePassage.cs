using GameInterface.Services.MobilePartyAIs.Patches;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Barters.Handlers;

internal sealed partial class LordBarterHandler
{
    private static (
        float OfferValue,
        IReadOnlyList<MobileParty> OpponentParties) EvaluateSafePassageOffer(
        BarterData barter,
        Hero playerHero,
        MobileParty playerParty,
        Hero targetHero,
        MobileParty targetParty)
    {
        var parties = SafePassagePartyResolver.Resolve(playerParty, targetParty);
        var value = CalculateSafePassageOfferValue(
            barter,
            playerHero,
            playerParty,
            targetHero,
            targetParty,
            parties.PlayerSide,
            parties.OpponentSide);

        return (value, parties.OpponentSide);
    }

    private static float CalculateSafePassageOfferValue(
        BarterData barter,
        Hero playerHero,
        MobileParty playerParty,
        Hero targetHero,
        MobileParty targetParty,
        IEnumerable<MobileParty> playerSide,
        IEnumerable<MobileParty> opponentSide)
    {
        var offerValue = 0;
        foreach (var barterable in barter.GetOfferedBarterables())
        {
            offerValue += barterable is SafePassageBarterable
                ? CalculateSafePassageValue(
                    playerHero,
                    playerParty,
                    targetHero,
                    targetParty,
                    playerSide,
                    opponentSide)
                : barterable.GetValueForFaction(targetHero.Clan);
        }

        return offerValue;
    }

    // Mirrors SafePassageBarterable.GetUnitValueForFaction. The native method
    // cannot run on the dedicated server because it requires PlayerEncounter.
    private static int CalculateSafePassageValue(
        Hero playerHero,
        MobileParty playerParty,
        Hero targetHero,
        MobileParty targetParty,
        IEnumerable<MobileParty> playerSide,
        IEnumerable<MobileParty> opponentSide)
    {
        var strengthRatio = CalculateStrengthRatio(
            playerParty,
            playerSide,
            opponentSide);
        var wealth = CalculatePlayerWealth(playerHero, playerParty);
        var wealthFactor = CalculateWealthFactor(strengthRatio, playerParty);
        var relationFactor = CalculateRelationFactor(playerHero, targetHero);

        var price = (int)((wealth * wealthFactor) + 1000f);
        price /= 2;
        price += (int)(0.3f * wealthFactor *
                       Campaign.Current.Models.ValuationModel.GetMilitaryValueOfParty(targetParty));
        price += (int)(0.3f * wealthFactor *
                       Campaign.Current.Models.ValuationModel.GetValueOfHero(targetHero));
        if (playerHero.GetPerkValue(DefaultPerks.Trade.MarketDealer))
            price += MathF.Round(price * DefaultPerks.Trade.MarketDealer.PrimaryBonus);

        return -(int)(price / (relationFactor * relationFactor));
    }

    private static float CalculateStrengthRatio(
        MobileParty playerParty,
        IEnumerable<MobileParty> playerSide,
        IEnumerable<MobileParty> opponentSide)
    {
        var strengthContext = playerParty.IsCurrentlyAtSea
            ? MapEvent.PowerCalculationContext.SeaBattle
            : MapEvent.PowerCalculationContext.PlainBattle;
        var playerStrength = playerSide.Sum(party =>
            party.Party.GetCustomStrength(BattleSideEnum.Defender, strengthContext));
        var opponentStrength = opponentSide.Sum(party =>
            party.Party.GetCustomStrength(BattleSideEnum.Attacker, strengthContext));
        if (opponentStrength <= 0f)
            opponentStrength = 0.00001f;

        return MathF.Clamp(playerStrength / opponentStrength, 0f, 1f);
    }

    private static float CalculatePlayerWealth(Hero playerHero, MobileParty playerParty)
    {
        var wealth = (float)playerHero.Gold;
        foreach (var item in playerParty.ItemRoster)
        {
            wealth += (float)item.EquipmentElement.Item.Value * item.Amount;
            if (wealth >= int.MaxValue)
            {
                wealth = int.MaxValue;
                break;
            }
        }

        wealth = MathF.Clamp(wealth, 0f, int.MaxValue);
        return wealth + 3000f + (playerHero.Clan.Renown * 50f);
    }

    private static float CalculateWealthFactor(float strengthRatio, MobileParty playerParty)
    {
        var wealthFactor = strengthRatio < 1f
            ? 0.05f + ((1f - strengthRatio) * 0.2f)
            : 0.1f;
        wealthFactor *= 1.5f;
        if (playerParty.MapEvent != null || playerParty.SiegeEvent != null)
            wealthFactor *= 1.2f;

        return wealthFactor;
    }

    private static float CalculateRelationFactor(Hero playerHero, Hero targetHero)
    {
        return targetHero.Clan.Leader == null
            ? 1f
            : MathF.Clamp(
                (50f + targetHero.Clan.Leader.GetRelation(playerHero)) / 50f,
                0.05f,
                1.1f);
    }

    internal static void ApplySafePassage(
        MobileParty targetParty,
        MobileParty playerParty,
        IEnumerable<MobileParty> opponentParties)
    {
        if (targetParty == null || playerParty == null) return;
        var attackProtectionEnds = CampaignTime.HoursFromNow(32f);
        var factionProtectionEnds = CampaignTime.DaysFromNow(5f);
        var protectedParties = new HashSet<MobileParty>(
            opponentParties ?? Enumerable.Empty<MobileParty>());
        protectedParties.Add(targetParty);

        foreach (var party in protectedParties)
        {
            if (party == null) continue;
            DefaultMobilePartyAIModelPatches.PreventAttacksUntil(
                party,
                playerParty,
                attackProtectionEnds);
            party.SetMoveModeHold();
            party.IgnoreForHours(32f);
            party.Ai.SetInitiative(0f, 0.8f, 8f);
        }

        DefaultMobilePartyAIModelPatches.PreventFactionAttacksUntil(
            playerParty,
            targetParty.MapFaction,
            factionProtectionEnds);
    }
}
