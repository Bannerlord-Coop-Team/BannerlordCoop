using HarmonyLib;
using StoryMode.Behaviors.Quests.FirstPhase;

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

        [HarmonyPrefix]
        [HarmonyPatch("SetDialogs")]
        static bool PrefixSetDialogs()
        {
            return false;
        }
    }
}
