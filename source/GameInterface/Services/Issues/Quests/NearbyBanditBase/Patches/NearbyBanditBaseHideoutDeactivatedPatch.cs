using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using Quest = TaleWorlds.CampaignSystem.Issues.NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssueQuest;

namespace GameInterface.Services.Issues.Quests.NearbyBanditBase.Patches;

[HarmonyPatch]
internal class NearbyBanditBaseHideoutDeactivatedPatch
{
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnHideoutDeactivated))]
    [HarmonyPostfix]
    private static void OnHideoutDeactivatedPostfix(Settlement hideout)
    {
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return;
        }

        dispatcher.RunAsOwners<Quest>(quest => quest.OnHideoutCleared(hideout));
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnHideoutCleared))]
    [HarmonyPrefix]
    private static bool OnHideoutClearedPrefix()
    {
        return ModInformation.IsServer;
    }
}
