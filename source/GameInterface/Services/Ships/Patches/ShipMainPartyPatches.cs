using Common;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyBases.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Naval;

namespace GameInterface.Services.Ships.Patches;

/// <summary>
/// Replaces vanilla's PartyBase.MainParty checks on ships and keeps clients from writing server-owned piece state.
/// </summary>
[HarmonyPatch]
internal class ShipMainPartyPatches
{
    /// <summary>
    /// Rebuilds or clears the unlocked pieces, except on a client's managed ship which gets them in the pieces snapshot.
    /// </summary>
    [HarmonyPatch(typeof(Ship), nameof(Ship.CanHaveUnlockedPieces), MethodType.Setter)]
    [HarmonyPrefix]
    public static bool CanHaveUnlockedPiecesSetterPrefix(Ship __instance)
    {
        return !(ModInformation.IsClient && IsManaged(__instance));
    }

    /// <summary>
    /// v1.5 asks this event whether a ship that changed owner keeps unlocked pieces, and War Sails answers with a
    /// PartyBase.MainParty check. Every player's party counts instead, as for the stash and return details that
    /// always keep them.
    /// </summary>
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.CanHaveUnlockedUpgradePiece))]
    [HarmonyPostfix]
    public static void CanHaveUnlockedUpgradePiecePostfix(Ship ship, ref bool canHaveUpgradePiece)
    {
        if (!canHaveUpgradePiece && ship?.Owner.IsPlayerParty() == true)
            canHaveUpgradePiece = true;
    }

    // Mission-only ships are never registered, so their pieces stay local
    private static bool IsManaged(Ship ship)
    {
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return false;

        return objectManager.TryGetId(ship, out _);
    }
}
