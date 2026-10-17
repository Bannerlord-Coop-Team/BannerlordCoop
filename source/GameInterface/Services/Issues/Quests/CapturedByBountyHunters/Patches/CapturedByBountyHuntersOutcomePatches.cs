using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using Quest = TaleWorlds.CampaignSystem.Issues.CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssueQuest;

namespace GameInterface.Services.Issues.Quests.CapturedByBountyHunters.Patches;

/// <summary>
/// Only the server finalizes map events, so it checks every running quest's hideout battle as its owning player
/// and applies the consequences there. A client only ends its copy of the quest once the server announces it.
/// </summary>
[HarmonyPatch]
internal class CapturedByBountyHuntersOutcomePatches
{
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventEnded))]
    [HarmonyPostfix]
    private static void OnMapEventEndedPostfix(MapEvent mapEvent)
    {
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return;
        }

        dispatcher.RunAsOwners<Quest>(quest =>
        {
            // An owner without a party, a prisoner for one, would be checked against the party of whoever ended the battle
            if (Hero.MainHero?.PartyBelongedTo == null)
            {
                return;
            }

            quest.OnMapEventEnded(mapEvent);
        });
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnMapEventEnded))]
    [HarmonyPrefix]
    private static bool OnMapEventEndedPrefix()
    {
        return ModInformation.IsServer;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.SuccessConsequences))]
    [HarmonyPrefix]
    private static bool SuccessConsequencesPrefix()
    {
        return ModInformation.IsServer;
    }

    // The client replaying a timeout reaches this too, the server already applied it
    [HarmonyPatch(typeof(Quest), nameof(Quest.FailConsequences))]
    [HarmonyPrefix]
    private static bool FailConsequencesPrefix()
    {
        return ModInformation.IsServer;
    }
}
