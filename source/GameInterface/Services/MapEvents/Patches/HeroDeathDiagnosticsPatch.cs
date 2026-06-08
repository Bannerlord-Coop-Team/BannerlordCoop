using Common;
using Common.Logging;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// TEMPORARY DIAGNOSTIC. Logs the cause and call stack of every hero death so we can pinpoint where
/// auto-resolve hero kills actually originate. Log-only (returns void; does not affect the kill).
/// Remove once the battle-simulation hero-death path is identified.
/// </summary>
[HarmonyPatch(typeof(KillCharacterAction))]
internal class HeroDeathDiagnosticsPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<HeroDeathDiagnosticsPatch>();

    [HarmonyPatch("ApplyInternal")]
    [HarmonyPrefix]
    private static void Prefix_ApplyInternal(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail actionDetail)
    {
        if (victim == null)
            return;

        Logger.Warning(
            "[HeroDeathDiag] Hero death: Victim={Victim} Killer={Killer} Cause={Cause} IsServer={IsServer} SimulationActive={SimActive}\nStack:\n{Stack}",
            victim.Name?.ToString(),
            killer?.Name?.ToString() ?? "<none>",
            actionDetail,
            ModInformation.IsServer,
            PlayerEncounter.CurrentBattleSimulation != null,
            Environment.StackTrace);
    }
}
