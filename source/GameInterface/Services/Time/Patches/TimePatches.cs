using GameInterface.Services.Heroes.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Time.Patches
{
    [HarmonyPatch(typeof(Campaign))]
    internal class TimePatches
    {

        [HarmonyPatch("TimeControlMode")]
        [HarmonyPatch(MethodType.Setter)]
        static bool Prefix()
        {
            return !TimeControlInterface.TimeLock;
        }
    }
}
