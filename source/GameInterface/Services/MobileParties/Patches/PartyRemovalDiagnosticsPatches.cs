using Common;
using Common.Logging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using Serilog.Events;
using System.Threading;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Diagnostics for #3588: logs every party removal and destroy at Debug, and warns when a client removes a registered
/// party outside the receive path, which leaves it registered but gone locally while the server still counts it.
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
        bool debug = Logger.IsEnabled(LogEventLevel.Debug);
        bool isServer = ModInformation.IsServer;
        if (!debug && isServer) return;

        // Without Debug the id is only needed for a warning, so skip the lookup when none can fire.
        bool originalsAllowed = CallOriginalPolicy.IsOriginalAllowed();
        if (!debug && originalsAllowed) return;

        bool isRegistered = TryGetRegisteredId(__instance, out string partyId);
        if (debug) LogDebug("MobileParty.RemoveParty", partyId, originalsAllowed);
        if (ShouldWarn(isServer, isRegistered, originalsAllowed))
        {
            Logger.Warning("[PartyRemoval] Party {PartyId} was removed on this client without a server destroy message",
                partyId);
        }
    }

    [HarmonyPatch(typeof(DestroyPartyAction), nameof(DestroyPartyAction.ApplyInternal))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void ApplyInternalPrefix(MobileParty destroyedParty)
    {
        if (!Logger.IsEnabled(LogEventLevel.Debug)) return;

        TryGetRegisteredId(destroyedParty, out string partyId);
        LogDebug("DestroyPartyAction.ApplyInternal", partyId, CallOriginalPolicy.IsOriginalAllowed());
    }

    // Received destroys and allowed originals (AllowedThread, save load) are expected, and so is a client
    // destroying a party the server never registered, like a quest spawned one.
    internal static bool ShouldWarn(bool isServer, bool isRegistered, bool originalsAllowed) =>
        !isServer && isRegistered && !originalsAllowed;

    private static void LogDebug(string operation, string partyId, bool originalsAllowed)
    {
        Logger.Debug("[PartyRemoval] {Operation} party={PartyId} thread={ThreadId} allowedThread={AllowedThread} originalsAllowed={OriginalsAllowed}",
            operation,
            partyId,
            Thread.CurrentThread.ManagedThreadId,
            AllowedThread.IsThisThreadAllowed(),
            originalsAllowed);
    }

    private static bool TryGetRegisteredId(MobileParty party, out string partyId)
    {
        if (party != null && ContainerProvider.TryResolve<IObjectManager>(out var objectManager)
            && objectManager.TryGetId(party, out partyId))
            return true;

        partyId = party == null ? "none" : party.StringId ?? "unregistered";
        return false;
    }
}
