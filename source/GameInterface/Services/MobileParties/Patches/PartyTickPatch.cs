using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(CampaignPeriodicEventManager))]
internal class PartyTickPatch
{
    [HarmonyPatch(nameof(CampaignPeriodicEventManager.TickPeriodicEvents))]
    [HarmonyPrefix]
    static bool TickPeriodicEventsPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(CampaignPeriodicEventManager.MobilePartyHourlyTick))]
    [HarmonyPrefix]
    static bool MobilePartyHourlyTickPrefix() => ModInformation.IsServer;
}
