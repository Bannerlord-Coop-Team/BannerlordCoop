using HarmonyLib;
using SandBox.ViewModelCollection;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Uses the live mission result for the field-battle scoreboard when the campaign map event still carries
/// a retreat flag from before the mission ended.
/// </summary>
[HarmonyPatch(typeof(SPScoreboardVM), nameof(SPScoreboardVM.OnBattleOver))]
internal class BattleScoreboardResultPatch
{
    [HarmonyPostfix]
    private static void CorrectDepletionVictoryText(SPScoreboardVM __instance)
    {
        if (!BattleSpawnConfig.Enabled || !BattleSpawnGate.IsCoopBattleActive)
            return;

        var result = Mission.Current?.MissionResult;
        if (result == null || !result.PlayerVictory || result.EnemyRetreated)
            return;

        var mapEvent = PlayerEncounter.Battle;
        if (mapEvent?.IsFieldBattle != true || !mapEvent.EndedByRetreat)
            return;

        __instance.BattleResult = GameTexts.FindText("str_victory").ToString();
    }
}
