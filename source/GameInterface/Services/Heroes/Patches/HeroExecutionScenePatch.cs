using Common;
using Common.Messaging;
using GameInterface.Services.Party.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.SceneInformationPopupTypes;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(HeroExecutionSceneNotificationData))]
internal class HeroExecutionScenePatch
{
    [HarmonyPatch(nameof(HeroExecutionSceneNotificationData.PostponedAffirmativeAction))]
    [HarmonyPrefix]
    public static bool PostponedAffirmativeActionPrefix(HeroExecutionSceneNotificationData __instance)
    {
        if (ModInformation.IsServer) return false;

        if (__instance._runAffirmativeActionAtClose)
        {
            if (__instance._onAffirmativeAction != null)
            {
                __instance._onAffirmativeAction();
            }
            else if (__instance.Victim != Hero.MainHero)
            {
                // Don't use MapEvent to determine execution. MapEvent already finalized when this runs
                var message = new HeroExecuted(__instance.Victim, __instance.Executer, KillCharacterAction.KillCharacterActionDetail.Executed, true);
                MessageBroker.Instance.Publish(__instance, message);
            }
        }
        __instance._runAffirmativeActionAtClose = false;

        return false;
    }
}
