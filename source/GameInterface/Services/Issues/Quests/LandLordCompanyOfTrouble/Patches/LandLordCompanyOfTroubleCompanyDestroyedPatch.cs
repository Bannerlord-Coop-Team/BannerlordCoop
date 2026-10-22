using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Quest = TaleWorlds.CampaignSystem.Issues.LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest;

namespace GameInterface.Services.Issues.Quests.LandLordCompanyOfTrouble.Patches;

[HarmonyPatch]
internal class LandLordCompanyOfTroubleCompanyDestroyedPatch
{
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMobilePartyDestroyed))]
    [HarmonyPostfix]
    private static void OnMobilePartyDestroyedPostfix(MobileParty mobileParty, PartyBase destroyerParty)
    {
        if (!mobileParty.IsCurrentlyUsedByAQuest || !ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return;
        }

        dispatcher.RunAsOwners<Quest>(quest => OnPartyDestroyed(quest, mobileParty, destroyerParty));
    }

    private static void OnPartyDestroyed(Quest quest, MobileParty mobileParty, PartyBase destroyerParty)
    {
        if (quest._companyOfTroubleParty != mobileParty || destroyerParty == PartyBase.MainParty)
        {
            return;
        }

        // DestroyPartyAction is still running for this party, so the finalize cleanup must not destroy it again
        quest._companyOfTroubleParty = null;
        quest.RelationshipChangeWithQuestGiver = -2;
        quest.AddLog(quest.QuestFailCompanyLeft);
        quest.CompleteQuestWithFail();
    }
}
