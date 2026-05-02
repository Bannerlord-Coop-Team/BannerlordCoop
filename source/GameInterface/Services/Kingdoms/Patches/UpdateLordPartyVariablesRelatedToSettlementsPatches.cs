using Common;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Patches;

[HarmonyPatch(typeof(KingdomManager))]
internal class UpdateLordPartyVariablesRelatedToSettlementsPatches
{
    [HarmonyPatch(nameof(KingdomManager.UpdateLordPartyVariablesRelatedToSettlements))]
    [HarmonyPrefix]
    static bool Prefix() => ModInformation.IsServer;
}
