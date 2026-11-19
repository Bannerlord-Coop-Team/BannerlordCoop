using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Visibility;

/// <summary>Clients advance their own issues while the server owns expiry and foreign missions.</summary>
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(IssueManager), nameof(IssueManager.DailyTick))]
internal class ClientIssueDailyTickPatch
{
    [HarmonyPrefix]
    private static bool Prefix(IssueManager __instance)
    {
        if (ModInformation.IsServer) return true;
        if (ContainerProvider.TryResolve<IClientIssueDailyTickService>(out var service))
            service.Apply(__instance);
        return false;
    }
}
