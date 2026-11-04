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
/// Diagnostics for #3588: logs every party removal and destroy at Debug, and warns when a client removes a party
/// outside the receive path, which leaves it registered but gone locally while the server still counts it.
/// </summary>
[HarmonyPatch]
internal class PartyRemovalDiagnosticsPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyRemovalDiagnosticsPatches>();

    [HarmonyPatch(typeof(MobileParty), nameof(MobileParty.RemoveParty))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void RemovePartyPrefix(MobileParty __instance)
    {
        bool debug = Logger.IsEnabled(LogEventLevel.Debug);
        if (!debug && ModInformation.IsServer) return;

        string partyId = GetPartyId(__instance);
        if (debug) LogDebug("MobileParty.RemoveParty", partyId);
        WarnIfClientLocalRemoval(partyId);
    }

    [HarmonyPatch(typeof(DestroyPartyAction), nameof(DestroyPartyAction.ApplyInternal))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void ApplyInternalPrefix(MobileParty destroyedParty)
    {
        if (!Logger.IsEnabled(LogEventLevel.Debug)) return;

        LogDebug("DestroyPartyAction.ApplyInternal", GetPartyId(destroyedParty));
    }

    // Received destroys are applied under AllowedThread, so a client removal outside it never came from the server.
    internal static bool WarnIfClientLocalRemoval(string partyId)
    {
        if (ModInformation.IsServer || AllowedThread.IsThisThreadAllowed()) return false;

        Logger.Warning("[PartyRemoval] Party {PartyId} was removed on this client without a server destroy message",
            partyId);
        return true;
    }

    private static void LogDebug(string operation, string partyId)
    {
        Logger.Debug("[PartyRemoval] {Operation} party={PartyId} thread={ThreadId} allowedThread={AllowedThread} originalsAllowed={OriginalsAllowed}",
            operation,
            partyId,
            Thread.CurrentThread.ManagedThreadId,
            AllowedThread.IsThisThreadAllowed(),
            CallOriginalPolicy.IsOriginalAllowed());
    }

    private static string GetPartyId(MobileParty party)
    {
        if (party == null) return "none";
        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) && objectManager.TryGetId(party, out string id))
            return id;
        return party.StringId ?? "unregistered";
    }
}
