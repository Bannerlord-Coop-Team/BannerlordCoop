using Common.Logging;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using Serilog;
using System;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(DisbandPartyAction))]
internal class DisbandPartyActionPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<DisbandPartyActionPatches>();

    [HarmonyPatch(nameof(DisbandPartyAction.StartDisband))]
    [HarmonyPrefix]
    public static bool PrefixStartDisband(MobileParty disbandParty)
    {
        if (disbandParty == null || !disbandParty.IsPlayerParty()) return true;

        Logger.Warning("Blocked DisbandPartyAction.StartDisband for player party {StringId}", disbandParty.StringId);

        return false;
    }

    // Vanilla StartDisband without the prefix above, for coop.debug.mobileparty.start_disband
    [HarmonyReversePatch]
    [HarmonyPatch(nameof(DisbandPartyAction.StartDisband))]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void StartDisbandUnguarded(MobileParty disbandParty) =>
        throw new NotSupportedException("StartDisband reverse patch was not installed.");
}
