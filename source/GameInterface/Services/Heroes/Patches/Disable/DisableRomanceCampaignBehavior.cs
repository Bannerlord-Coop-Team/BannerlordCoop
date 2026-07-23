using Common;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Heroes.Patches.Disable;

[HarmonyPatch(typeof(RomanceCampaignBehavior))]
internal class DisableRomanceCampaignBehavior
{
    static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
    {
        AccessTools.Method(typeof(RomanceCampaignBehavior), nameof(RomanceCampaignBehavior.DailyTick)),
        AccessTools.Method(typeof(RomanceCampaignBehavior), nameof(RomanceCampaignBehavior.DailyTickClan))
    };

    [HarmonyPrefix]
    static bool Prefix() => ModInformation.IsServer;
}
