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
        if (ModInformation.IsServer && ContainerProvider.TryResolve<IOutcomeDispatcher>(out _))
        {
            Prepare(__instance.MapEvent);
        }
    }

    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.SimulateBattleSetup))]
    [HarmonyPrefix]
    private static void PrepareSimulation(MapEvent __instance)
    {
        if (ModInformation.IsServer)
        {
            Prepare(__instance);
        }
    }

    private static void Prepare(MapEvent battle)
    {
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return;
        }

        dispatcher.RunAsOwners<Quest>(quest =>
        {
            if (!quest.IsOngoing || quest._lesserNobleParty == null ||
                !battle.InvolvedParties.Contains(quest._lesserNobleParty.Party) ||
                !battle.InvolvedParties.Contains(quest.QuestGiver.PartyBelongedTo?.Party))
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
