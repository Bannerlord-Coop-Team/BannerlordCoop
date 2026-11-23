using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Registry.Auto;
using GameInterface.Services.MobilePartyAIs.Messages;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using Serilog.Events;
using System.Threading;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobilePartyAIs.Patches;


[HarmonyPatch]
class MobilePartyAiLifetimePatches
{
    static readonly ILogger Logger = LogManager.GetLogger<MobilePartyAiLifetimePatches>();

    [HarmonyPatch(typeof(MobilePartyAi), MethodType.Constructor, typeof(MobileParty))]
    [HarmonyPostfix]
    private static void CtorPostfix(MobilePartyAi __instance, MobileParty mobileParty)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created managed {name}", typeof(MobilePartyAi));
            return;
        }

        MessageBroker.Instance.Publish(__instance, new MobilePartyAiCreated(__instance, mobileParty));
    }

    // #3588: logs every party removal at Debug.
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

    [HarmonyPatch(typeof(MobileParty), nameof(MobileParty.RemoveParty))]
    [HarmonyPostfix]
    internal static void RemoveParty_Postfix(ref MobileParty __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            // #3588: the caller shows which client code removed a party the server still counts.
            Logger.Error("Client destroyed unmanaged {name} {PartyId}. Caller: {Caller}",
                typeof(MobileParty), GetPartyId(__instance), System.Environment.StackTrace);
            return;
        }

        var ai = __instance.Ai;

        MessageBroker.Instance.Publish(ai, new InstanceDestroyed<MobilePartyAi>(ai));
    }

    /// <summary>The registered id, or the party's StringId marked unregistered when the server never registered it.</summary>
    private static string GetPartyId(MobileParty party)
    {
        if (party == null) return "none";

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager)
            && objectManager.TryGetId(party, out string partyId))
            return partyId;

        return $"{party.StringId ?? "no StringId"} (unregistered)";
    }
}
