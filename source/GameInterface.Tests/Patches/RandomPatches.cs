using System;
using HarmonyLib;
using TaleWorlds.Core;

namespace GameInterface.Tests.Patches
{
    [HarmonyPatch(typeof(MBRandom), nameof(MBRandom.RandomInt), new Type[] { })]
    public class RandomIntPatch
    {
        public static bool Prefix(ref int __result)
        {
            Random random = new Random();
            __result =  random.Next();

            return false;
        }
    }
    
    [HarmonyPatch(typeof(MBRandom), nameof(MBRandom.DeterministicRandomInt))]
    public class DeterministicRandomIntPatch
    {
        public static bool Prefix(int maxValue, ref int __result)
        {
            Random random = new Random();
            __result =  random.Next() % maxValue;

            return false;
        }
    }
}