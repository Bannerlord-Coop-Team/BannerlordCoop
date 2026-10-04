using Common;
using Common.Messaging;
using GameInterface.Services.CampaignService.Handlers;
using GameInterface.Services.Heroes.HeirSelection.Messages;
using GameInterface.Services.Players.Messages;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;

namespace GameInterface.Services.Players.Patches;

[HarmonyPatch(typeof(RetirementCampaignBehavior))]
internal class RetirementCampaignBehaviorPatches
{
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
            MessageBroker.Instance.Publish(__instance, new PlayerRetirementRequested());

            GameMenu.ExitToLast();
            __instance.ShowGameStatistics();
        }

        return false;
    }

    private const string AnswerContinueId = "hermit_answer_continue_1";

    [HarmonyPatch(nameof(RetirementCampaignBehavior.SetupConversationDialogues))]
    [HarmonyPostfix]
    public static void SetupConversationDialoguesPostfix(RetirementCampaignBehavior __instance)
    {
        var targetSentence = Campaign.Current.ConversationManager._sentences.FirstOrDefault(sentence => sentence.Id == AnswerContinueId);

        // Don't assign new consequence delegate if the sentence isn't found
        if (targetSentence == null) return;

        targetSentence.OnConsequence = delegate()
        {
            __instance._hasTalkedWithHermitBefore = true;
            MessageBroker.Instance.Publish(__instance, new UpdateHasMetHermit(Hero.MainHero, true));
        };
    }

    [HarmonyPatch(nameof(RetirementCampaignBehavior.DecideRetirementPositively))]
    [HarmonyPostfix]
    public static void DecideRetirementPositivelyPostfix(RetirementCampaignBehavior __instance)
    {
        if (!__instance._hasTalkedWithHermitBefore)
        {
            MessageBroker.Instance.Publish(__instance, new UpdateHasMetHermit(Hero.MainHero, false));
        }
    }
}
