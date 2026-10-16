using Common;
using Common.Messaging;
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

        var food = FindFood();
        if (food == null)
        {
            return false;
        }

        if (ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker))
        {
            messageBroker.Publish(__instance, new LandLordCompanyOfTroubleFoodStolen(__instance, food));
        }

        ShowTheft(__instance);
        __instance._thieveryCount++;
        return false;
    }

    private static ItemObject FindFood()
    {
        var roster = MobileParty.MainParty.ItemRoster;

        for (int i = 0; i < roster.Count; i++)
        {
            var element = roster[i];
            var item = element.EquipmentElement.Item;

            if (!element.IsEmpty && item.IsFood)
            {
                return item;
            }
        }

        return null;
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
