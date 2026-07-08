using Common;
using Common.Logging;
using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using Serilog;
using System.Diagnostics;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// TEMP diagnostic: logs a stack trace whenever a player-controlled party's CurrentSettlement is set to a
/// fortification, to find where a co-op besieger gets marked as inside the town it is besieging (vanilla keeps
/// a besieger outside, CurrentSettlement == null). Patches the setter so it catches every write path (an action,
/// a direct set, or an AutoSync apply). Runs on both sides so we can see which one sets it. Remove once root-caused.
/// </summary>
[HarmonyPatch(typeof(MobileParty), nameof(MobileParty.CurrentSettlement), MethodType.Setter)]
internal class CurrentSettlementWatcherPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<CurrentSettlementWatcherPatch>();

    [HarmonyPrefix]
    private static void Prefix(MobileParty __instance, Settlement value)
    {
        if (value == null || !value.IsFortification) return;
        if (__instance?.LeaderHero == null || !__instance.LeaderHero.IsPlayerHero()) return;

        Logger.Information("[SettleWatch] {Side} {Party} CurrentSettlement <- {New} (was {Old}) besieging={Besieging} behavior={Behavior}\n{Stack}",
            ModInformation.IsServer ? "SERVER" : "CLIENT",
            __instance.Name?.ToString(),
            value.Name?.ToString(),
            __instance.CurrentSettlement?.Name?.ToString(),
            __instance.BesiegedSettlement?.Name?.ToString(),
            __instance.DefaultBehavior,
            new StackTrace(true).ToString());
    }
}
