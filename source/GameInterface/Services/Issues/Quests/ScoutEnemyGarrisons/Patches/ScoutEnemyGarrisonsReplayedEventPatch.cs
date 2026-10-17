using Common;
using GameInterface.Policies;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using Quest = TaleWorlds.CampaignSystem.Issues.ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsQuest;

namespace GameInterface.Services.Issues.Quests.ScoutEnemyGarrisons.Patches;

/// <summary>
/// Clients fire army dispersal and settlement owner changes while applying the server's message under
/// AllowedThread, where the dispatcher would let the quest cancel locally without telling the server.
/// The owner's quest reacts on the next frame instead, so its cancel is handed to the server.
/// </summary>
[HarmonyPatch]
internal class ScoutEnemyGarrisonsReplayedEventPatch
{
    [ThreadStatic]
    private static bool isDeferredRun;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnArmyDispersed))]
    [HarmonyPrefix]
    private static bool OnArmyDispersedPrefix(Quest __instance, Army army, Army.ArmyDispersionReason reason, bool isPlayersArmy)
    {
        return RunOutsideReplay(__instance, () => __instance.OnArmyDispersed(army, reason, isPlayersArmy));
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnSettlementOwnerChanged))]
    [HarmonyPrefix]
    private static bool OnSettlementOwnerChangedPrefix(
        Quest __instance,
        Settlement settlement,
        bool openToClaim,
        Hero newOwner,
        Hero oldOwner,
        Hero capturerHero,
        ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
    {
        return RunOutsideReplay(__instance, () => __instance.OnSettlementOwnerChanged(settlement, openToClaim, newOwner, oldOwner, capturerHero, detail));
    }

    private static bool RunOutsideReplay(Quest quest, Action listener)
    {
        if (ModInformation.IsServer || isDeferredRun || !CallOriginalPolicy.IsOriginalAllowed())
        {
            return true;
        }

        GameThread.EnqueueSafe(() =>
        {
            if (!quest.IsOngoing)
            {
                return;
            }

            isDeferredRun = true;
            try
            {
                listener();
            }
            finally
            {
                isDeferredRun = false;
            }
        }, context: nameof(ScoutEnemyGarrisonsReplayedEventPatch));

        return false;
    }
}
