using Common;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.MobileParties.Patches.Disable;

[HarmonyPatch(typeof(PartiesSellPrisonerCampaignBehavior))]
internal class DisablePartiesSellPrisonerCampaignBehavior
{
    [HarmonyPatch(nameof(PartiesSellPrisonerCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}

[HarmonyPatch(typeof(PartiesSellPrisonerCampaignBehavior))]
internal class PartiesSellPrisonerCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(PartiesSellPrisonerCampaignBehavior.OnSettlementEntered))]
    [HarmonyPrefix]
    public static bool OnSettlementEnteredPrefix(PartiesSellPrisonerCampaignBehavior __instance, MobileParty mobileParty, Settlement settlement, Hero hero)
    {
        // Replace IsMainParty check
        if (mobileParty != null && !mobileParty.IsPlayerParty()
            && settlement.IsFortification && mobileParty.MapFaction != null
            && !mobileParty.IsDisbanding && !mobileParty.MapFaction.IsAtWarWith(settlement.MapFaction)
            && (mobileParty.PrisonRoster.TotalRegulars > 0 || (mobileParty.PrisonRoster.TotalHeroes > 0
            && mobileParty.PrisonRoster.GetTroopRoster().Exists((TroopRosterElement x) => (!x.Character.IsHero || !x.Character.HeroObject.IsPlayerHero()) // Replace PlayerCharacter check
            && x.Character.HeroObject.MapFaction.IsAtWarWith(settlement.MapFaction)))))
        {
            TroopRoster troopRoster = TroopRoster.CreateDummyTroopRoster();
            foreach (TroopRosterElement troopRosterElement in mobileParty.PrisonRoster.GetTroopRoster())
            {
                // Replace IsPlayerCharacter check
                if (!troopRosterElement.Character.IsHero || (!troopRosterElement.Character.HeroObject.IsPlayerHero() && troopRosterElement.Character.HeroObject.MapFaction.IsAtWarWith(settlement.MapFaction)))
                {
                    troopRoster.Add(troopRosterElement);
                }
            }
            SellPrisonersAction.ApplyForSelectedPrisoners(mobileParty.Party, settlement.Party, troopRoster);
        }

        return false;
    }

    [HarmonyPatch(nameof(PartiesSellPrisonerCampaignBehavior.DailyTickSettlement))]
    [HarmonyPrefix]
    public static bool DailyTickSettlementPrefix(PartiesSellPrisonerCampaignBehavior __instance, Settlement settlement)
    {
        if (!settlement.IsFortification) return false;

        TroopRoster prisonRoster = settlement.Party.PrisonRoster;
        if (prisonRoster.TotalRegulars <= 0) return false;

        // Replace MainHero check
        int num = (settlement.Owner.IsPlayerHero()) ? (prisonRoster.TotalManCount - settlement.Party.PrisonerSizeLimit) : MBRandom.RoundRandomized((float)prisonRoster.TotalRegulars * 0.1f);
        if (num <= 0) return false;

        TroopRoster troopRoster = TroopRoster.CreateDummyTroopRoster();
        IEnumerable<TroopRosterElement> enumerable;
        if (!settlement.Owner.IsPlayerHero()) // Replace MainHero check
        {
            enumerable = prisonRoster.GetTroopRoster().AsEnumerable<TroopRosterElement>();
        }
        else
        {
            IEnumerable<TroopRosterElement> enumerable2 = from t in prisonRoster.GetTroopRoster()
                                                          orderby t.Character.Tier
                                                          select t;
            enumerable = enumerable2;
        }
        foreach (TroopRosterElement troopRosterElement in enumerable)
        {
            if (!troopRosterElement.Character.IsHero)
            {
                int num2 = Math.Min(num, troopRosterElement.Number);
                num -= num2;
                troopRoster.AddToCounts(troopRosterElement.Character, num2, false, 0, 0, true, -1);
                if (num <= 0)
                {
                    break;
                }
            }
        }
        if (troopRoster.TotalManCount > 0)
        {
            SellPrisonersAction.ApplyForSelectedPrisoners(settlement.Party, null, troopRoster);
        }

        return false;
    }
}