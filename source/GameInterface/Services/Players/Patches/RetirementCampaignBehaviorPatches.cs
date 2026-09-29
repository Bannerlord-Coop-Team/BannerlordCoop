using Common;
using Common.Messaging;
using GameInterface.Services.CampaignService.Handlers;
using GameInterface.Services.Heroes.HeirSelection.Messages;
using GameInterface.Services.Players.Messages;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;

namespace GameInterface.Services.Players.Patches;

[HarmonyPatch(typeof(RetirementCampaignBehavior))]
internal class RetirementCampaignBehaviorPatches
{
#if TESTER
    [HarmonyPatch(nameof(RetirementCampaignBehavior.OnSessionLaunched))]
    [HarmonyPrefix]
    public static bool OnSessionLaunchedPrefix() => ModInformation.IsClient;

    [HarmonyPatch(nameof(RetirementCampaignBehavior.HourlyTick))]
    [HarmonyPrefix]
    public static bool HourlyTickPrefix() => ModInformation.IsClient;

    [HarmonyPatch(nameof(RetirementCampaignBehavior.GameMenuOpened))]
    [HarmonyPrefix]
    public static bool GameMenuOpenedPrefix(RetirementCampaignBehavior __instance, MenuCallbackArgs args)
    {
        if (ModInformation.IsServer) return false;
        if (args.MenuContext.GameMenu.StringId != "retirement_place") return false;

        if (__instance._selectedHeir != null)
        {
            PlayerEncounter.Finish(true);

            MessageBroker.Instance.Publish(__instance, new HeirSelectedForRetirement(Hero.MainHero, __instance._selectedHeir));

            __instance._selectedHeir = null;
            return false;
        }
        if (__instance._playerEndedGame)
        {
            GameOverState.IsGameOver = true;
            MessageBroker.Instance.Publish(__instance, new PlayerDeleteRequested(true));

            GameMenu.ExitToLast();
            __instance.ShowGameStatistics();
        }

        return false;
    }
#else
    [HarmonyPatch(nameof(RetirementCampaignBehavior.RegisterEvents))]
    [HarmonyPrefix]
    public static bool Prefix() => false;
#endif
}
