using Common;
using GameInterface.Services.Clans.Interfaces;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Clans.Patches.Disable;

[HarmonyPatch(typeof(ClanVariablesCampaignBehavior))]
internal class DisableClanVariablesCampaignBehavior
{
    static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
    {
        AccessTools.Method(typeof(ClanVariablesCampaignBehavior), nameof(ClanVariablesCampaignBehavior.DailyTickHero)),
        AccessTools.Method(typeof(ClanVariablesCampaignBehavior), nameof(ClanVariablesCampaignBehavior.OnSettlementOwnerChanged)),
        AccessTools.Method(typeof(ClanVariablesCampaignBehavior), nameof(ClanVariablesCampaignBehavior.OnClanChangedKingdom)),
        AccessTools.Method(typeof(ClanVariablesCampaignBehavior), nameof(ClanVariablesCampaignBehavior.OnHeroChangedClan)),
        AccessTools.Method(typeof(ClanVariablesCampaignBehavior), nameof(ClanVariablesCampaignBehavior.OnGameLoadFinished)),
        AccessTools.Method(typeof(ClanVariablesCampaignBehavior), nameof(ClanVariablesCampaignBehavior.WeeklyTickClan)),
    };


    // Disable on client
    [HarmonyPrefix]
    static bool Prefix() => ModInformation.IsServer;
}

[HarmonyPatch(typeof(ClanVariablesCampaignBehavior))]
internal class ClanVariablesCampaignBehaviorPatches
{
    /// <summary>
    /// Runs the coop clan tick on the server only, running vanilla when no session container is available.
    /// </summary>
    [HarmonyPatch(nameof(ClanVariablesCampaignBehavior.DailyTickClan))]
    [HarmonyPrefix]
    public static bool DailyTickClanPrefix(ClanVariablesCampaignBehavior __instance, Clan clan)
    {
        if (!ContainerProvider.TryResolve<IClanVariablesCampaignBehaviorInterface>(out var clanVariablesCampaignBehaviorInterface)) return true;

        if (ModInformation.IsClient) return false;

        clanVariablesCampaignBehaviorInterface.DailyTickClan(__instance, clan);

        return false;
    }
}