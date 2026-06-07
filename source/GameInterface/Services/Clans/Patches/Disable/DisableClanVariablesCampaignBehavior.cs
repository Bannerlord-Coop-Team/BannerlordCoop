using Common;
using GameInterface.Services.Heroes.Messages;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;
using GameInterface.Policies;

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
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return ModInformation.IsServer;
    }
}
