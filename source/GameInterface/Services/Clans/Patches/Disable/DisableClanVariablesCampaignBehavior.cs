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
        AccessTools.Method(typeof(ClanVariablesCampaignBehavior), nameof(ClanVariablesCampaignBehavior.DailyTickClan)),
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
    [HarmonyPatch(nameof(ClanVariablesCampaignBehavior.DailyTickClan))]
    [HarmonyPrefix]
    public static bool DailyTickClanPrefix(ClanVariablesCampaignBehavior __instance, Clan clan)
    {
        ContainerProvider.TryResolve<IClanVariablesCampaignBehaviorInterface>(out var clanVariablesCampaignBehaviorInterface);

        clanVariablesCampaignBehaviorInterface.DailyTickClan(__instance, clan);

        return false;
    }
}