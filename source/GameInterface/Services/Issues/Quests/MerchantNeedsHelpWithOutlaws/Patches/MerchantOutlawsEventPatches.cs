using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Quest = TaleWorlds.CampaignSystem.Issues.MerchantNeedsHelpWithOutlawsIssueQuestBehavior.MerchantNeedsHelpWithOutlawsIssueQuest;

namespace GameInterface.Services.Issues.Quests.MerchantNeedsHelpWithOutlaws.Patches;

[HarmonyPatch]
internal class MerchantOutlawsEventPatches
{
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMobilePartyDestroyed))]
    [HarmonyPostfix]
    private static void DestroyedPostfix(MobileParty mobileParty, PartyBase destroyerParty)
    {
        RunAsOwners(quest =>
        {
            if (!quest._validPartiesList.Contains(mobileParty)) return;
            RemoveParty(quest, mobileParty);
            if (Hero.MainHero.PartyBelongedTo?.Party == destroyerParty && destroyerParty != null)
            {
                quest._destroyedPartyCount++;
                quest.AddQuestStepLog();
            }
            else
            {
                Send(quest);
            }
        });
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnBanditPartyRecruited))]
    [HarmonyPostfix]
    private static void RecruitedPostfix(MobileParty banditParty)
    {
        var recruitingParty = MobileParty.MainParty;
        RunAsOwners(quest =>
        {
            if (!quest._validPartiesList.Contains(banditParty)) return;
            RemoveParty(quest, banditParty);
            if (Hero.MainHero.PartyBelongedTo == recruitingParty)
            {
                quest._recruitedPartyCount++;
                quest.AddQuestStepLog();
            }
            else
            {
                Send(quest);
            }
        });
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.HourlyTickParty))]
    [HarmonyPostfix]
    private static void HourlyPostfix(MobileParty mobileParty)
    {
        if (!mobileParty.IsBandit) return;
        RunAsOwners(quest =>
        {
            var wasValid = quest._validPartiesList.Contains(mobileParty);
            quest.HourlyTickParty(mobileParty);
            if (wasValid != quest._validPartiesList.Contains(mobileParty)) Send(quest);
        });
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnSettlementEntered))]
    [HarmonyPostfix]
    private static void EnteredPostfix(MobileParty party, Settlement settlement, Hero hero)
    {
        if (party == null || !party.IsBandit || !settlement.IsHideout) return;
        RunAsOwners(quest =>
        {
            if (quest._validPartiesList.Contains(party)) return;
            quest.OnSettlementEntered(party, settlement, hero);
            if (quest._validPartiesList.Contains(party)) Send(quest);
        });
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnSettlementLeft))]
    [HarmonyPostfix]
    private static void LeftPostfix(MobileParty party, Settlement settlement)
    {
        if (!settlement.IsHideout) return;
        RunAsOwners(quest =>
        {
            if (!quest._validPartiesList.Contains(party)) return;
            quest.OnSettlementLeft(party, settlement);
            if (!quest._validPartiesList.Contains(party)) Send(quest);
        });
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnVillageBeingRaided))]
    [HarmonyPostfix]
    private static void RaidedPostfix(Village village) => RunAsOwners(quest => quest.OnVillageRaided(village));

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventStarted))]
    [HarmonyPostfix]
    private static void MapEventPostfix(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
    {
        RunAsOwners(quest => quest.OnMapEventStarted(mapEvent, attackerParty, defenderParty));
    }

    // Server mirrors never register events; the owning client's listeners leave world actions to the server.
    [HarmonyPatch(typeof(Quest), nameof(Quest.MobilePartyDestroyed))]
    [HarmonyPrefix]
    private static bool DestroyedPrefix() => false;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnBanditPartyRecruited))]
    [HarmonyPrefix]
    private static bool RecruitedPrefix() => false;

    [HarmonyPatch(typeof(Quest), nameof(Quest.HourlyTickParty))]
    [HarmonyPrefix]
    private static bool HourlyPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnSettlementEntered))]
    [HarmonyPrefix]
    private static bool EnteredPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnSettlementLeft))]
    [HarmonyPrefix]
    private static bool LeftPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnVillageRaided))]
    [HarmonyPrefix]
    private static bool RaidedPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnMapEventStarted))]
    [HarmonyPrefix]
    private static bool MapEventPrefix() => ModInformation.IsServer;

    private static void RemoveParty(Quest quest, MobileParty party)
    {
        quest._validPartiesList.RemoveAll(candidate => candidate == party);
        if (quest.IsTracked(party)) quest.RemoveTrackedObject(party);
    }

    private static void Send(Quest quest)
    {
        if (ContainerProvider.TryResolve<IMerchantOutlawsProgress>(out var progress)) progress.Send(quest);
    }

    private static void RunAsOwners(Action<Quest> listener)
    {
        if (ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher)) dispatcher.RunAsOwners(listener);
    }
}
