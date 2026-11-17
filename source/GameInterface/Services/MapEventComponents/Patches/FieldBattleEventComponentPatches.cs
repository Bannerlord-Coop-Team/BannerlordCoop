using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventComponents.Patches;

/// <summary>
/// Keeps the field battle settlement the server set, instead of letting a client derive its own.
/// </summary>
[HarmonyPatch(typeof(FieldBattleEventComponent), nameof(FieldBattleEventComponent.UpdateMapEventSettlement))]
internal class FieldBattleEventComponentPatches
{
    // A client only reaches this from the load rerun, where vanilla would pick a nearby village for the client's own MainParty.
    [HarmonyPrefix]
    private static bool UpdateMapEventSettlementPrefix() => ModInformation.IsServer;
}
