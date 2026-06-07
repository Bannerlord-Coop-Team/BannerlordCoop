using Common;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using GameInterface.Policies;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(CampaignPeriodicEventManager))]
internal class PartyTickPatch
{
    [HarmonyPatch(nameof(CampaignPeriodicEventManager.TickPeriodicEvents))]
    [HarmonyPrefix]
    static bool TickPeriodicEventsPrefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(CampaignPeriodicEventManager.MobilePartyHourlyTick))]
    [HarmonyPrefix]
    static bool MobilePartyHourlyTickPrefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return ModInformation.IsServer;
    }
}
