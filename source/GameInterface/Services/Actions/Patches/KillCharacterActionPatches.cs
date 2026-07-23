using Common;
using Common.Logging;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Actions.Patches;

[HarmonyPatch(typeof(KillCharacterAction))]
internal class KillCharacterActionPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<KillCharacterActionPatches>();

    [HarmonyPatch(nameof(KillCharacterAction.ApplyInternal))]
    static bool Prefix() => ModInformation.IsServer;
}
