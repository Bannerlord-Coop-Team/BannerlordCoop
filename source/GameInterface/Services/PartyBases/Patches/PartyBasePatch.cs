﻿using Common.Logging;
using Common.Messaging;
using GameInterface.Services.ItemRosters;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.PartyBases.Patches
{
    [HarmonyPatch(typeof(PartyBase))]
    internal class PartyBasePatch
    {
        [HarmonyPatch(nameof(PartyBase.ItemRoster), MethodType.Setter)]
        [HarmonyPostfix]
        public static void ItemRosterSetterPostfix(ref PartyBase __instance)
        {
            if (ModInformation.IsClient) return;

            ItemRosterLookup.Set(__instance.ItemRoster, __instance);
        }
    }
}
