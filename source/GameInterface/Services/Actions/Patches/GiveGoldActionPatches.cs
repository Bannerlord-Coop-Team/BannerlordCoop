using Common;
using Common.Logging;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Actions.Patches;

[HarmonyPatch(typeof(GiveGoldAction))]
internal class GiveGoldActionPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<GiveGoldActionPatches>();

    [HarmonyPatch(nameof(GiveGoldAction.ApplyInternal))]
    static bool Prefix() => ModInformation.IsServer;
}
