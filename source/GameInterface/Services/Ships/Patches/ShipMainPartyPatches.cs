using GameInterface.Services.PartyBases.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Ships.Patches;

[HarmonyPatch(typeof(Ship))]
internal class ShipMainPartyPatches
{
    [HarmonyPatch(nameof(Ship.ResetUnlockedUpgradePieces))]
    [HarmonyPrefix]
    public static bool ResetUnlockedUpgradePiecesPrefix(Ship __instance)
    {
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
}
