using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch(typeof(PlayerEncounter))]
internal class PlayerEncounterPatches
{
    [HarmonyPatch(nameof(PlayerEncounter.CheckNearbyPartiesToJoinPlayerMapEvent))]
    [HarmonyPrefix]
    private static bool PrefixCheckNearbyPartiesToJoinPlayerMapEvent()
    {
        return false;
    }
}
