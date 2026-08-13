#if DEBUG
using Common.Logging;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(MobileParty), nameof(MobileParty.ComputePathAfterLoad))]
internal class MobilePartyLoadStateDiagnosticPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<MobilePartyLoadStateDiagnosticPatch>();

    [HarmonyPrefix]
    private static void RecordPathComputationStarted(MobileParty __instance)
    {
        if (!ContainerProvider.TryResolve<IMobilePartyLoadStateDiagnostic>(out var diagnostic))
        {
            Logger.Error("Unable to resolve {Diagnostic}", nameof(IMobilePartyLoadStateDiagnostic));
            return;
        }

        diagnostic.RecordStarted(__instance);
    }

    [HarmonyPostfix]
    private static void RecordPathComputationCompleted(MobileParty __instance)
    {
        if (!ContainerProvider.TryResolve<IMobilePartyLoadStateDiagnostic>(out var diagnostic))
        {
            Logger.Error("Unable to resolve {Diagnostic}", nameof(IMobilePartyLoadStateDiagnostic));
            return;
        }

        diagnostic.RecordCompleted(__instance);
    }
}
#endif
