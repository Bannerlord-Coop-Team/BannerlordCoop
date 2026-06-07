using Common;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using SandBox.View.Map;
using SandBox.View.Map.Managers;
using SandBox.View.Map.Visuals;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using GameInterface.Policies;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch]
class DebugPatches
{
    [HarmonyPatch(typeof(MobileParty), nameof(MobileParty.Position), MethodType.Setter)]
    [HarmonyPostfix]
    static void Postfix_IsLastSpeedCacheInvalid(MobileParty __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        if (__instance.IsPlayerParty() || __instance == MobileParty.MainParty)
        {
            ;
        }
    }
}
