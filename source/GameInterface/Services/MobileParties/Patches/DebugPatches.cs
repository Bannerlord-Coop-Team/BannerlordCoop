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

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch]
class DebugPatches
{
    [HarmonyPatch(typeof(MobileParty), nameof(MobileParty.Position), MethodType.Setter)]
    [HarmonyPostfix]
    static void Postfix_IsLastSpeedCacheInvalid(MobileParty __instance)
    {
        if (__instance.IsPlayer() || __instance == MobileParty.MainParty)
        {
            ;
        }
    }
}
