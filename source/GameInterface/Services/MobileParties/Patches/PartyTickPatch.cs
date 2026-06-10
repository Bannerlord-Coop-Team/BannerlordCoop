using Common;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch]
internal class PartyTickPatch
{
    [HarmonyPatch(typeof(CampaignPeriodicEventManager), nameof(CampaignPeriodicEventManager.TickPeriodicEvents))]
    [HarmonyPrefix]
    static bool Prefix_TickPeriodicEvents() => ModInformation.IsServer;

    [HarmonyPatch(typeof(CampaignPeriodicEventManager), nameof(CampaignPeriodicEventManager.MobilePartyHourlyTick))]
    [HarmonyPrefix]
    static bool Prefix_MobilePartyHourlyTick() => ModInformation.IsServer;


    [HarmonyPatch(typeof(MobileParty), nameof(MobileParty.HourlyTick))]
    [HarmonyPrefix]
    static bool Prefix_HourlyTick() => ModInformation.IsServer;
}