using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest;

namespace GameInterface.Services.Issues.Quests.LandLordCompanyOfTrouble.Patches;

[HarmonyPatch]
internal class LandLordCompanyOfTroubleFoodTheftPatch
{
    [HarmonyPatch(typeof(Quest), nameof(Quest.TryToStealItemFromPlayer))]
    [HarmonyPrefix]
    private static bool TryToStealItemFromPlayerPrefix(Quest __instance)
    {
        if (ModInformation.IsServer)
        {
            return true;
        }

        if (!HasFood())
        {
            return false;
        }

        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher) ||
            dispatcher.BeforeQuestBranch(__instance, (byte)LandLordCompanyOfTroubleBranch.FoodStolen))
        {
            return true;
        }

        ShowTheft(__instance);
        __instance._thieveryCount++;
        return false;
    }

    private static bool HasFood()
    {
        return MobileParty.MainParty.ItemRoster.TotalFood > 0;
    }

    private static void ShowTheft(Quest quest)
    {
        if (quest._thieveryCount > 1)
        {
            MBInformationManager.AddQuickInformation(new TextObject("{=xlm8oYhM}Your men reported that some of the goods were stolen from the baggage train."));
            return;
        }

        var text = quest._thieveryCount == 0
            ? new TextObject("{=OKpwA8Az}Your men have noticed some of the goods in the baggage train are missing.")
            : new TextObject("{=acu1wTeq}Your men are sure of that some of the goods were stolen from the baggage train.");

        InformationManager.ShowInquiry(
            new InquiryData(quest.Title.ToString(), text.ToString(), true, false, GameTexts.FindText("str_ok").ToString(), "", null, null),
            true);
    }
}
