using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using Quest = TaleWorlds.CampaignSystem.Issues.NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssueQuest;

namespace GameInterface.Services.Issues.Quests.NearbyBanditBase.Patches;

[HarmonyPatch]
internal class NearbyBanditBaseMapEventStartedPatch
{
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventStarted))]
    [HarmonyPostfix]
    private static void OnMapEventStartedPostfix(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
    {
        if (!(mapEvent.IsForcingSupplies || mapEvent.IsForcingVolunteers) || !ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return;
        }

        dispatcher.RunAsOwners<Quest>(quest => quest.OnMapEventStarted(mapEvent, attackerParty, defenderParty));
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnMapEventStarted))]
    [HarmonyPrefix]
    private static bool OnMapEventStartedPrefix()
    {
        return ModInformation.IsServer;
    }
}
