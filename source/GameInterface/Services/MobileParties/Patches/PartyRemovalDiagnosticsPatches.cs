using Common;
using Common.Logging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using Serilog.Events;
using System.Threading;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Diagnostics for #3588: logs every party removal at Debug. A client only removal is logged as an error with the
/// party id and caller by MobilePartyAiLifetimePatches.
/// </summary>
[HarmonyPatch]
internal class PartyRemovalDiagnosticsPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyRemovalDiagnosticsPatches>();

    [HarmonyPatch(typeof(MobileParty), nameof(MobileParty.RemoveParty))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    internal static void RemovePartyPrefix(MobileParty __instance)
    {
        if (!Logger.IsEnabled(LogEventLevel.Debug)) return;

        Logger.Debug("[PartyRemoval] MobileParty.RemoveParty party={PartyId} thread={ThreadId} allowedThread={AllowedThread} originalsAllowed={OriginalsAllowed}",
            GetPartyId(__instance),
            Thread.CurrentThread.ManagedThreadId,
            AllowedThread.IsThisThreadAllowed(),
            CallOriginalPolicy.IsOriginalAllowed());
    }

    /// <summary>The registered id, or the party's StringId marked unregistered when the server never registered it.</summary>
    internal static string GetPartyId(MobileParty party)
    {
        if (party == null) return "none";

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager)
            && objectManager.TryGetId(party, out string partyId))
            return partyId;

        return $"{party.StringId ?? "no StringId"} (unregistered)";
    }
}
