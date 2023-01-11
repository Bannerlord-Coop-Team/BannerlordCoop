using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Patch
{
    [HarmonyPatch(typeof(Hero))]
    internal class HeroPatches
    {

        [HarmonyPostfix]
        [HarmonyPatch(MethodType.Constructor)]
        static void Postfix(Hero __instance)
        {
            
            //if(Coop.IsServer)
            //{
            //    string stacktrace = Environment.StackTrace;
            //    Logger.Info($"Creating new hero, {__instance.Name}");
            //}
            //else if(CoopClient.Instance.ClientPlaying)
            //{
            //    string stacktrace = Environment.StackTrace;
            //    Logger.Info($"Creating new hero, {__instance.Name}");
            //}
            
        }
    }
}
