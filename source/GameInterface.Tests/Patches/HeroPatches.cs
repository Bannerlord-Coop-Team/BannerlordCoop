using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Tests.Patches
{
    [HarmonyPatch(typeof(Hero), nameof(Hero.ChangeState))]
    public class HeroChangeStatePatch
    {
        public static bool Prefix()
        {
            return false;
        }
    }
    
    [HarmonyPatch(typeof(Hero), nameof(Hero.Init))]
    public class HeroInitPatch
    {
        public static bool Prefix(ref Hero __instance)
        {
            __instance.RandomValue = MBRandom.RandomInt();
            __instance.RandomValueDeterministic = MBRandom.DeterministicRandomInt(100);
            __instance.RandomValueRarelyChanging = MBRandom.DeterministicRandomInt(100);
            
            return false;
        }
    }
}