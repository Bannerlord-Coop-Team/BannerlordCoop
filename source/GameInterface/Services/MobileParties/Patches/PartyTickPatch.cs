using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(CampaignPeriodicEventManager))]
internal class PartyTickPatch
{
    [HarmonyPatch(nameof(CampaignPeriodicEventManager.MobilePartyHourlyTick))]
    static bool Prefix() => ModInformation.IsServer;
}
