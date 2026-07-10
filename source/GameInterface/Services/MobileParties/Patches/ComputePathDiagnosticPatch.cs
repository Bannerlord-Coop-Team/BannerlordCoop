using Common.Logging;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Diagnostic: a MobileParty whose ComputePath returns false moves in a straight pathless
/// line toward its target — through rivers, towns, anything. Vanilla can barely hit this
/// (its map screen clamps click targets first); in coop it has fired twice via unclamped
/// click targets and invalid stand positions, so every new occurrence logs its reason.
/// </summary>
[HarmonyPatch(typeof(MobileParty), "ComputePath")]
internal class ComputePathDiagnosticPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<MobileParty>();
    private static DateTime _nextLog = DateTime.MinValue;

    static void Postfix(MobileParty __instance, CampaignVec2 newTargetPosition, bool __result)
    {
        if (__result) return;
        // Throttled: a stuck party retries every tick and would flood the log.
        if (DateTime.UtcNow < _nextLog) return;
        _nextLog = DateTime.UtcNow.AddSeconds(1);

        string reason;
        if (!__instance.Position.IsValid())
        {
            reason = "party position invalid";
        }
        else if (!newTargetPosition.IsValid())
        {
            reason = "target position invalid";
        }
        else
        {
            var terrain = Campaign.Current?.MapSceneWrapper?.GetFaceTerrainType(newTargetPosition.Face);
            reason = $"no route (target terrain {terrain})";
        }

        Logger.Warning(
            "ComputePath FAILED for {Party} ({X:0.#},{Y:0.#}) -> ({TX:0.#},{TY:0.#}): {Reason} — party will move in a straight line",
            __instance.StringId, __instance.Position.X, __instance.Position.Y,
            newTargetPosition.X, newTargetPosition.Y, reason);
    }
}
