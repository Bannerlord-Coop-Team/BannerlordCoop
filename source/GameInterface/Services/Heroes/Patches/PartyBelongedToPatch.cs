using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Heroes.Patches
{
    [HarmonyPatch(typeof(Hero))]
    public class PartyBelongedToPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(Hero.PartyBelongedToAsPrisoner), MethodType.Setter)]
        private static bool PrefixBelongedAsPrisoner(ref Hero __instance, ref PartyBase value)
        {
            return true;
        }
        [HarmonyPrefix]
        [HarmonyPatch(nameof(Hero.PartyBelongedTo), MethodType.Setter)]
        private static bool PrefixBelonged(ref Hero __instance, ref PartyBase value)
        {
            return true;
        }
    }
}
