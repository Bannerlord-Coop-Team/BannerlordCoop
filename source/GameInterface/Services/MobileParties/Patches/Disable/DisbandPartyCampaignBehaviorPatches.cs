using Common;
using GameInterface.Services.Clans.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Patches.Disable;

[HarmonyPatch(typeof(DisbandPartyCampaignBehavior))]
internal class DisbandPartyCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(DisbandPartyCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;

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
        if (mobileParty.IsCaravan && mobileParty.ActualClan.IsPlayerClan() && !mobileParty.IsDisbanding && __instance._partiesThatWaitingToDisband.ContainsKey(mobileParty) && mobileParty.CurrentSettlement == null && mobileParty.TargetSettlement != null)
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