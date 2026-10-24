using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using Quest = TaleWorlds.CampaignSystem.Issues.LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest;

namespace GameInterface.Services.Issues.Quests.LandLordCompanyOfTrouble.Patches;

[HarmonyPatch]
internal class LandLordCompanyOfTroubleMapEventEndedPatch
{
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventEnded))]
    [HarmonyPostfix]
    private static void OnMapEventEndedPostfix(MapEvent mapEvent)
    {
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return;
        }

        dispatcher.RunAsOwners<Quest>(quest => OnMapEventEnded(quest, mapEvent));
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnMapEventEnded))]
    [HarmonyPrefix]
    private static bool OnMapEventEndedPrefix()
    {
        return ModInformation.IsServer;
    }

    private static void OnMapEventEnded(Quest quest, MapEvent mapEvent)
    {
        var involvedParties = mapEvent.InvolvedParties.ToList();
        if (!involvedParties.Contains(PartyBase.MainParty))
        {
            return;
        }

        var company = quest._companyOfTroubleParty;
        if (company == null)
        {
            quest.OnMapEventEnded(mapEvent);
            return;
        }

        if (!involvedParties.Contains(company.Party))
        {
            return;
        }

        if (mapEvent.WinningSide == PartyBase.MainParty.Side)
        {
            quest.QuestSuccessWithPlayerDefeatedCompany();
        }
        else
        {
            quest.QuestFailWithPlayerDefeatedAgainstCompany();
        }
    }
}
