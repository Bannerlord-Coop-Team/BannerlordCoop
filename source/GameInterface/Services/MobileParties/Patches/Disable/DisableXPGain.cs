using HarmonyLib;
using SandBox.Issues;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MobileParties.Patches.Disable
{
    [HarmonyPatch(typeof(MapEventParty))]
    internal class DisableXPGain
    {
        [HarmonyPatch(nameof(MapEventParty.CommitXpGain))]
        static bool Prefix() => false;
    }
}
