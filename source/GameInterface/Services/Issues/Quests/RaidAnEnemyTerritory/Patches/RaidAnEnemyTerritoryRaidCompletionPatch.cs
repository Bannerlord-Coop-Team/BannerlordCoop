using Common;
using Common.Util;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Quest = TaleWorlds.CampaignSystem.Issues.RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest;

namespace GameInterface.Services.Issues.Quests.RaidAnEnemyTerritory.Patches;

[HarmonyPatch]
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
internal sealed class RaidAnEnemyTerritoryRaidCompletionPatch
{
    [HarmonyPatch(typeof(Quest), nameof(Quest.OnRaidCompleted))]
    [HarmonyPrefix]
    // RaidCompletedPostfix credits the owner on the server.
    private static bool OnRaidCompletedPrefix() => false;

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.RaidCompleted))]
    [HarmonyPostfix]
    private static void RaidCompletedPostfix(BattleSideEnum winnerSide, RaidEventComponent raidEvent)
    {
        if (ModInformation.IsClient) return;

        var mapEvent = raidEvent.MapEvent;
        var village = mapEvent.MapEventSettlement;
        var lootedVillage = winnerSide == BattleSideEnum.Attacker && village.IsVillage && village.SettlementHitPoints == 0f;
        if (!lootedVillage ||
            !ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher) ||
            !ContainerProvider.TryResolve<IRaidAnEnemyTerritoryProgress>(out var progress))
        {
            return;
        }

        // Count progress as each owner without ending the quest or releasing a party inside the map event.
        GameThread.RunSafe(() =>
        {
            var attackers = new HashSet<MobileParty>(mapEvent.AttackerSide.Parties
                .Select(party => party.Party?.MobileParty).OfType<MobileParty>());
            dispatcher.RunAsOwners<Quest>(quest => progress.RecordCompletedRaid(quest, village, attackers));
        }, context: nameof(RaidCompletedPostfix));
    }
}
