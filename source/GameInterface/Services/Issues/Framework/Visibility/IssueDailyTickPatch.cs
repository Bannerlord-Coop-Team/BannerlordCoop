using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Visibility;

/// <summary>
/// The only patch on the issue daily tick. Clients advance their own issues, the server retries failed endings after it.
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
    private static void Postfix(IssueManager __instance)
    {
        if (ModInformation.IsClient) return;

        if (ContainerProvider.TryResolve<IIssueEndingCoordinator>(out var endings)) endings.RetryFailedEndings();
        MessageBroker.Instance.Publish(__instance, new IssueManagerDailyTicked());
    }
}
