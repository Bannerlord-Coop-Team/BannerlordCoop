using Common;
using Common.Messaging;
using GameInterface.Services.Party.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.SceneInformationPopupTypes;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(HeroExecutionSceneNotificationData))]
internal class HeroExecutionScenePatch
{
    [HarmonyPatch(nameof(HeroExecutionSceneNotificationData.PostponedAffirmativeAction))]
    [HarmonyPrefix]
    public static bool PostponedAffirmativeActionPrefix(HeroExecutionSceneNotificationData __instance)
    {
        if (ModInformation.IsServer) return false;

        if (__instance._runAffirmativeActionAtClose && !__instance._isVisualOnly)
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

    [HarmonyPatch(nameof(HeroExecutionSceneNotificationData.GetExecuteTroopDescriptionText))]
    [HarmonyPrefix]
    public static bool GetExecuteTroopDescriptionTextPrefix(Hero dyingHero, ref TextObject __result)
    {
        __result = dyingHero.Clan == null ? null : GameTexts.FindText("str_execute_prisoner_desc_blood_feud");
        return false;
    }

    [HarmonyPatch(nameof(HeroExecutionSceneNotificationData.GetExecuteTroopHintText))]
    [HarmonyPrefix]
    public static bool GetExecuteTroopHintTextPrefix(ref TextObject __result)
    {
        __result = TextObject.GetEmpty();
        return false;
    }
}
