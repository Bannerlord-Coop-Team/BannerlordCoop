using Common;
using Common.Logging;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Actions.Patches;

[HarmonyPatch(typeof(ChangeRelationAction))]
internal class ChangeRelationActionPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<ChangeRelationActionPatches>();

    [HarmonyPatch(nameof(ChangeRelationAction.ApplyInternal))]
    static bool Prefix() => ModInformation.IsServer;
}
