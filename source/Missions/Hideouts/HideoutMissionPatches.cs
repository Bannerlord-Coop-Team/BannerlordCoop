using HarmonyLib;
using SandBox.CampaignBehaviors;
using SandBox.Missions;
using SandBox.Missions.MissionLogics.Hideout;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace Missions.Hideouts;

[HarmonyPatch]
[HarmonyPatchCategory(MissionModule.HideoutPatchCategory)]
internal static class HideoutDepletionPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(HideoutMissionController), nameof(HideoutMissionController.IsSideDepleted));
        yield return AccessTools.Method(typeof(HideoutAmbushMissionController), nameof(HideoutAmbushMissionController.IsSideDepleted));
    }

    private static bool Prefix(BattleSideEnum side, ref bool __result)
    {
        var coop = Mission.Current?.GetMissionBehavior<CoopHideoutMissionLogic>();
        if (coop == null) return true;
        __result = coop.IsSideDepleted(side);
        return false;
    }
}

[HarmonyPatch(typeof(HideoutAmbushMissionController), nameof(HideoutAmbushMissionController.OnStealthMissionCounterFailed))]
[HarmonyPatchCategory(MissionModule.HideoutPatchCategory)]
internal static class HideoutStealthFailurePatch
{
    private static bool Prefix()
    {
        var coop = Mission.Current?.GetMissionBehavior<CoopHideoutMissionLogic>();
        if (coop == null) return true;
        coop.OnStealthFailure();
        return false;
    }
}

[HarmonyPatch(typeof(StealthFailCounterMissionLogic), nameof(StealthFailCounterMissionLogic.ShowMissionFailedPopup))]
[HarmonyPatchCategory(MissionModule.HideoutPatchCategory)]
internal static class HideoutStealthFailurePopupPatch
{
    private static bool Prefix()
    {
        var coop = Mission.Current?.GetMissionBehavior<CoopHideoutMissionLogic>();
        if (coop == null) return true;
        coop.OnStealthFailure();
        return false;
    }
}

[HarmonyPatch(typeof(HideoutConversationsCampaignBehavior))]
[HarmonyPatchCategory(MissionModule.HideoutPatchCategory)]
internal static class HideoutBossDuelPatches
{
    private const string DuelLineId = "bandit_hideout_start_defender_1";

    // Native's duel answer has no condition, so attach one that hides it while other players share the mission.
    [HarmonyPatch(nameof(HideoutConversationsCampaignBehavior.AddDialogs))]
    [HarmonyPostfix]
    private static void HideDuelWithOtherPlayers()
    {
        var line = Campaign.Current?.ConversationManager?._sentences.FirstOrDefault(sentence => sentence.Id == DuelLineId);
        if (line != null)
            line.OnCondition = () => !HasOtherPlayers();
    }

    // Native only counts AI companions, so the battle answer must stay clickable when the other fighters are players.
    [HarmonyPatch(nameof(HideoutConversationsCampaignBehavior.bandit_hideout_continue_battle_on_clickable_condition))]
    [HarmonyPostfix]
    private static void AllowBattleWithOtherPlayers(ref bool __result, ref TextObject explanation)
    {
        if (__result || !HasOtherPlayers()) return;
        __result = true;
        explanation = TextObject.GetEmpty();
    }

    private static bool HasOtherPlayers()
        => Mission.Current?.GetMissionBehavior<CoopHideoutMissionLogic>()?.HasOtherPlayers == true;
}

[HarmonyPatch(typeof(HideoutAmbushMissionController), nameof(HideoutAmbushMissionController.SpawnRemainingTroopsForBossFight))]
[HarmonyPatchCategory(MissionModule.HideoutPatchCategory)]
internal static class HideoutBossReservePatch
{
    internal static void Prefix(HideoutAmbushMissionController __instance, ref int spawnCount)
    {
        // Native pads with unregistered PartyGroupAgentOrigin clones; the raid only fields its real reserve.
        if (__instance is CoopHideoutAmbushController)
            spawnCount = System.Math.Min(spawnCount, __instance._allEnemyTroops.Count);
    }
}
