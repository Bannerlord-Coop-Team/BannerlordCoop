using Common;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyBases.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Ships.Patches;

/// <summary>
/// Replaces vanilla's PartyBase.MainParty checks on ships and keeps clients from writing server-owned piece state.
/// </summary>
[HarmonyPatch(typeof(Ship))]
internal class ShipMainPartyPatches
{
    /// <summary>
    /// Rebuilds the unlocked pieces, except on a client's managed ship which gets them in the pieces snapshot.
    /// </summary>
    [HarmonyPatch(nameof(Ship.ResetUnlockedUpgradePieces))]
    [HarmonyPrefix]
    public static bool ResetUnlockedUpgradePiecesPrefix(Ship __instance)
    {
        if (ModInformation.IsClient && IsManaged(__instance)) return false;

        // Replace PartyBase.MainParty usage
        if (__instance.Owner.IsPlayerParty())
        {
            __instance._unlockedUpgradePieces = new MBList<ShipUpgradePiece>(__instance._shipPieces.Count);

            foreach (var pair in __instance._shipPieces)
            {
                if (pair.Value != null)
                {
                    __instance._unlockedUpgradePieces.Add(pair.Value);
                }
            }
            return false;
        }

        __instance._unlockedUpgradePieces = null;
        return false;
    }

    /// <summary>
    /// Equips a piece and unlocks it for player-owned ships.
    /// </summary>
    [HarmonyPatch(nameof(Ship.EquipUpgradePiece))]
    [HarmonyPrefix]
    public static bool EquipUpgradePiecePrefix(Ship __instance, string slotTag, ShipUpgradePiece newUpgradePiece)
    {
        __instance.GetPieceAtSlot(slotTag);

        // Replace PartyBase.MainParty usage
        if (__instance.Owner.IsPlayerParty() && newUpgradePiece != null)
        {
            __instance.AddToUnlockedPieces(newUpgradePiece);
        }
        __instance.SetPieceAtSlot(slotTag, newUpgradePiece);

        return false;
    }

    // Mission-only ships are never registered, so their pieces stay local
    private static bool IsManaged(Ship ship)
    {
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return false;

        return objectManager.TryGetId(ship, out _);
    }
}
