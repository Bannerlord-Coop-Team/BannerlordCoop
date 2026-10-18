using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using System.Linq;
using TaleWorlds.Core;
using Quest = TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssueQuest;

namespace GameInterface.Services.Issues.Quests.LesserNobleRevolt.Patches;

[HarmonyPatch]
internal static class LesserNobleRevoltBattlePatches
{
    [HarmonyPatch(typeof(MapEventSide), nameof(MapEventSide.MakeReadyForMission))]
    [HarmonyPrefix]
    private static void PrepareMission(MapEventSide __instance)
    {
        if (ModInformation.IsServer)
        {
            StripMilitia(__instance.MapEvent);
        }
    }

    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.SimulateBattleSetup))]
    [HarmonyPrefix]
    private static void PrepareSimulation(MapEvent __instance)
    {
        if (ModInformation.IsServer)
        {
            StripMilitia(__instance);
        }
    }

    private static void StripMilitia(MapEvent battle)
    {
        // AI battles are simulated every tick, so only a battle with a quest party looks for a revolt.
        if (!battle.InvolvedParties.Any(party => party.IsMobile && party.MobileParty.IsCurrentlyUsedByAQuest) ||
            !ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return;
        }

        dispatcher.RunAsOwners<Quest>(quest =>
        {
            // Like vanilla, for any player's battle with the revolt party: AI parties ignore it.
            if (!quest.IsOngoing || quest._lesserNobleParty == null ||
                !battle.InvolvedParties.Contains(quest._lesserNobleParty.Party))
            {
                return;
            }

            foreach (var party in battle.InvolvedParties.ToList())
            {
                if (party.IsMobile && party.MobileParty.IsMilitia)
                {
                    party.MapEventSide = null;
                }
            }
        });
    }
}
