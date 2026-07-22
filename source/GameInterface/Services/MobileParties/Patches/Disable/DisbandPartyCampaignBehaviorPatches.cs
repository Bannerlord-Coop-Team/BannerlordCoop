using Common;
using GameInterface.Services.Clans.Extensions;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Patches.Disable;

[HarmonyPatch(typeof(DisbandPartyCampaignBehavior))]
internal class DisablePartyCampaignBehaviorPatch
{
    private static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
    {
        AccessTools.Method(typeof(DisbandPartyCampaignBehavior), nameof(DisbandPartyCampaignBehavior.OnGameLoadFinished)),
        AccessTools.Method(typeof(DisbandPartyCampaignBehavior), nameof(DisbandPartyCampaignBehavior.OnPartyDisbandStarted)),
        AccessTools.Method(typeof(DisbandPartyCampaignBehavior), nameof(DisbandPartyCampaignBehavior.OnPartyDisbandCanceled)),
        AccessTools.Method(typeof(DisbandPartyCampaignBehavior), nameof(DisbandPartyCampaignBehavior.HourlyTick)),
        AccessTools.Method(typeof(DisbandPartyCampaignBehavior), nameof(DisbandPartyCampaignBehavior.OnMobilePartyDestroyed)),
        AccessTools.Method(typeof(DisbandPartyCampaignBehavior), nameof(DisbandPartyCampaignBehavior.OnHeroTeleportationRequested)),
        //AccessTools.Method(typeof(DisbandPartyCampaignBehavior), nameof(DisbandPartyCampaignBehavior.OnHeroPrisonerTaken)),
        AccessTools.Method(typeof(DisbandPartyCampaignBehavior), nameof(DisbandPartyCampaignBehavior.OnPartyDisbanded)),
        //AccessTools.Method(typeof(DisbandPartyCampaignBehavior), nameof(DisbandPartyCampaignBehavior.OnSessionLaunched)), Needed on client to load dialogue for interacting with disbanding parties
        AccessTools.Method(typeof(DisbandPartyCampaignBehavior), nameof(DisbandPartyCampaignBehavior.DailyTickParty)),
        AccessTools.Method(typeof(DisbandPartyCampaignBehavior), nameof(DisbandPartyCampaignBehavior.OnSettlementLeft)),
        AccessTools.Method(typeof(DisbandPartyCampaignBehavior), nameof(DisbandPartyCampaignBehavior.HourlyTickParty))
    };

    static bool Prefix()
    {
        return ModInformation.IsServer;
    }
}

[HarmonyPatch(typeof(DisbandPartyCampaignBehavior))]
internal class DisbandPartyCampaignBehaviorPatches
{
    /// <summary>
    /// Replaces several Clan.PlayerClan checks with IsPlayerClan() extension method
    /// </summary>
    [HarmonyPatch(nameof(DisbandPartyCampaignBehavior.OnPartyDisbandStarted))]
    [HarmonyPrefix]
    public static bool OnPartyDisbandStartedPrefix(ref DisbandPartyCampaignBehavior __instance, MobileParty party)
    {
        if (party.ActualClan.IsPlayerClan() || party.MemberRoster.Count < 10)
        {
            if (party.IsCaravan && party.ActualClan.IsPlayerClan())
            {
                party.Ai.SetDoNotMakeNewDecisions(true);
                __instance.GetTargetSettlementForDisbandingParty(party, out Settlement settlement, out MobileParty.NavigationType navigationType, out bool isTargetingThePort);
                if (settlement != null)
                {
                    party.SetMoveGoToSettlement(settlement, navigationType, isTargetingThePort);
                }
            }
            if (party.ActualClan.IsPlayerClan() && party.LeaderHero != null)
            {
                party.RemovePartyLeader();
            }
            CampaignTime value = party.IsCurrentlyAtSea ? CampaignTime.Never : CampaignTime.DaysFromNow(1f);
            __instance._partiesThatWaitingToDisband.Add(party, value);
            return false;
        }
        Hero hero = null;
        foreach (Hero hero2 in party.ActualClan.Heroes)
        {
            if (hero2.PartyBelongedTo == null && hero2.IsActive && hero2.DeathMark == KillCharacterAction.KillCharacterActionDetail.None && hero2.CurrentSettlement != null && hero2.GovernorOf == null && (!hero2.CurrentSettlement.IsUnderSiege || !hero2.CurrentSettlement.IsUnderRaid))
            {
                hero = hero2;
                break;
            }
        }
        if (hero != null)
        {
            TeleportHeroAction.ApplyDelayedTeleportToPartyAsPartyLeader(hero, party);
            return false;
        }
        __instance._partiesThatWaitingToDisband.Add(party, CampaignTime.DaysFromNow(1f));

        return false;
    }

    [HarmonyPatch(nameof(DisbandPartyCampaignBehavior.OnSettlementLeft))]
    [HarmonyPrefix]
    public static bool OnSettlementLeftPrefix(ref DisbandPartyCampaignBehavior __instance, MobileParty mobileParty, Settlement settlement)
    {
        // IsPlayerClan() replacement for Clan.PlayerClan
        if (mobileParty.IsCaravan && mobileParty.ActualClan?.IsPlayerClan() == true && !mobileParty.IsDisbanding && __instance._partiesThatWaitingToDisband.ContainsKey(mobileParty) && mobileParty.CurrentSettlement == null && mobileParty.TargetSettlement != null)
        {
            __instance.GetTargetSettlementForDisbandingParty(mobileParty, out Settlement settlement2, out MobileParty.NavigationType navigationType, out bool isTargetingThePort);
            if (settlement2 != null)
            {
                mobileParty.SetMoveGoToSettlement(settlement2, navigationType, isTargetingThePort);
            }
        }

        return false;
    }
}