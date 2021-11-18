using HarmonyLib;
using static StoryMode.Behaviors.Quests.FirstPhase.BannerInvestigationQuestBehavior;

namespace Coop.Mod.Patch
{
    [HarmonyPatch(typeof(BannerInvestigationQuest))]
    class BannerInvestigationQuestPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch("OnStartQuest")]
        static bool PrefixOnStartQuest()
        {
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("RegisterEvents")]
        static bool PrefixRegisterEvents()
        {
            return false;
        }
    }
}
