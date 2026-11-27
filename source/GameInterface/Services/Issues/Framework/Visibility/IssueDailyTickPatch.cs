using Common;
using Common.Logging;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Visibility;

/// <summary>
/// Clients advance their own issues while the server owns expiry and foreign missions. After the server's tick, which
/// skips an issue whose ending threw, that issue is taken out of the manager so the clients still hear about it.
/// </summary>
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(IssueManager), nameof(IssueManager.DailyTick))]
internal class IssueDailyTickPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<IssueDailyTickPatch>();

    [HarmonyPrefix]
    private static bool Prefix(IssueManager __instance)
    {
        if (ModInformation.IsServer) return true;
        if (!ContainerProvider.TryResolve<IClientIssueDailyTickService>(out var service))
        {
            Logger.Warning("Client issue daily tick service is unavailable, running the vanilla tick");
            return true;
        }

        service.Apply(__instance);
        return false;
    }

    [HarmonyPostfix]
    private static void Postfix()
    {
        if (ModInformation.IsClient || !ContainerProvider.TryResolve<IIssueEndingCoordinator>(out var endings)) return;

        endings.RetryFailedEndings();
    }
}
