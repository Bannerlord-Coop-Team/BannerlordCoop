using Common.Logging;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch]
internal class ArmyNamePatch
{
    [HarmonyPatch(typeof(MobileParty), nameof(MobileParty.ArmyName), MethodType.Getter)]
    [HarmonyPostfix]
    static void ArmyNamePostfix(ref TextObject __result)
    {
        if (__result == null)
            __result = TextObject.GetEmpty();
    }
}
