﻿using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Clans.Patches.Disable;

[HarmonyPatch(typeof(CrimeCampaignBehavior))]
internal class DisableCrimeCampaignBehavior
{
    [HarmonyPatch(nameof(CrimeCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
