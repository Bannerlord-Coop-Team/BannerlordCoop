using Common;
using HarmonyLib;
using SandBox.CampaignBehaviors;

namespace GameInterface.Services.UI.Notifications.Patches;

/// <summary>
/// Keeps campaign cutscenes off the server.
/// </summary>
/// <remarks>
/// Cutscenes are triggered by campaign events the server also processes - a marriage, for instance -
/// so a headless host would queue and play a wedding scene for two clients' heroes. There is nobody
/// to watch it, and it blocks the host while it runs. Clients still play their own.
/// </remarks>
[HarmonyPatch(typeof(DefaultCutscenesCampaignBehavior), nameof(DefaultCutscenesCampaignBehavior.RegisterEvents))]
internal static class DefaultCutscenesCampaignBehaviorPatches
{
    [HarmonyPrefix]
    private static bool RegisterEventsPrefix() => !ModInformation.IsServer;
}
