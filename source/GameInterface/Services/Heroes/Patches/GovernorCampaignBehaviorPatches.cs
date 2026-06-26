using Common;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(GovernorCampaignBehavior))]
internal class GovernorCampaignBehaviorPatches
{
    private static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
    {
        //AccessTools.Method(typeof(GovernorCampaignBehavior), nameof(GovernorCampaignBehavior.OnSessionLaunched)), // Needed to load dialogue for clients
        AccessTools.Method(typeof(GovernorCampaignBehavior), nameof(GovernorCampaignBehavior.OnHeroKilled)),
        AccessTools.Method(typeof(GovernorCampaignBehavior), nameof(GovernorCampaignBehavior.DailyTickSettlement)),
        AccessTools.Method(typeof(GovernorCampaignBehavior), nameof(GovernorCampaignBehavior.OnHeroChangedClan)),
        AccessTools.Method(typeof(GovernorCampaignBehavior), nameof(GovernorCampaignBehavior.OnGameLoadFinished))
    };

    static bool Prefix()
    {
        return ModInformation.IsServer;
    }
}