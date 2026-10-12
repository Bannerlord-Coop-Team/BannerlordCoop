using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Ships.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Naval;

namespace GameInterface.Services.Ships.Patches;

[HarmonyPatch(typeof(Ship))]
internal class ShipPiecesPatches
{
    [HarmonyPatch(nameof(Ship.InitializeFromTemplate))]
    [HarmonyPostfix]
    private static void Postfix_InitializeFromTemplate(Ship __instance) => PublishPieces(__instance);

    [HarmonyPatch(nameof(Ship.SetPieceAtSlot))]
    [HarmonyPostfix]
    private static void Postfix_SetPieceAtSlot(Ship __instance) => PublishPieces(__instance);

    [HarmonyPatch(nameof(Ship.ResetUnlockedUpgradePieces))]
    [HarmonyPostfix]
    private static void Postfix_ResetUnlockedUpgradePieces(Ship __instance) => PublishPieces(__instance);

    [HarmonyPatch(nameof(Ship.Figurehead), MethodType.Setter)]
    [HarmonyPostfix]
    private static void Postfix_SetFigurehead(Ship __instance) => __instance.UpdateVersionNo();

    private static void PublishPieces(Ship ship)
    {
        if (ModInformation.IsClient) return;

        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        MessageBroker.Instance.Publish(ship, new ShipPiecesChanged(ship));
    }
}
