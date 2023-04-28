using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Tests.Bootstrap.Patches
{
    [HarmonyPatch(typeof(Hero))]
    internal class HeroPatches
    {
        private static readonly FieldInfo _heroState = typeof(Hero).GetField("_heroState", BindingFlags.NonPublic | BindingFlags.Instance);
        [HarmonyPatch(nameof(Hero.ChangeState))]
        [HarmonyPrefix]
        private static bool ChangeStatePatch(ref Hero __instance, ref Hero.CharacterStates newState)
        {
            _heroState.SetValue(__instance, newState);
            return false;
        }
    }
}
