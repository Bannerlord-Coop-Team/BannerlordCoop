using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.Ships.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Ships.Patches;

/// <summary>
/// Replicates ship upgrade slots and figureheads, and makes unlocked upgrade pieces server-authoritative per
/// player-owned ship instead of tied to the local <c>PartyBase.MainParty</c>, which the server does not have.
/// </summary>
[HarmonyPatch(typeof(Ship))]
internal class ShipUpgradePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<ShipUpgradePatches>();

    // The single writer of _shipPieces after construction.
    [HarmonyPatch(nameof(Ship.SetPieceAtSlot))]
    [HarmonyPostfix]
    private static void SetPieceAtSlotPostfix(Ship __instance, string slotTag, ShipUpgradePiece upgradePiece)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client updated managed {member}", "Ship._shipPieces");
            return;
        }

        MessageBroker.Instance.Publish(__instance, new ShipUpgradePieceChanged(__instance, slotTag, upgradePiece));
    }

    [HarmonyPatch(nameof(Ship.ChangeFigurehead))]
    [HarmonyPostfix]
    private static void ChangeFigureheadPostfix(Ship __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client updated managed {member}", "Ship.Figurehead");
            return;
        }

        MessageBroker.Instance.Publish(__instance, new ShipFigureheadChanged(__instance));
    }

    // Vanilla unlocks the piece only for PartyBase.MainParty's ships; the server unlocks it for every player's ship.
    [HarmonyPatch(nameof(Ship.EquipUpgradePiece))]
    [HarmonyPrefix]
    private static bool EquipUpgradePiecePrefix(Ship __instance, string slotTag, ShipUpgradePiece newUpgradePiece)
    {
        if (ModInformation.IsClient) return true;

        // Vanilla's slot check, which throws for a slot the hull lacks.
        __instance.GetPieceAtSlot(slotTag);

        bool unlocked = newUpgradePiece != null && IsPlayerOwned(__instance);
        if (unlocked)
        {
            // A ship that became a player's before this rule existed has no list yet.
            if (__instance._unlockedUpgradePieces == null)
                __instance._unlockedUpgradePieces = CreateUnlockedPiecesFromEquipped(__instance);
            __instance.AddToUnlockedPieces(newUpgradePiece);
        }

        __instance.SetPieceAtSlot(slotTag, newUpgradePiece);

        if (unlocked && !CallOriginalPolicy.IsOriginalAllowed())
            MessageBroker.Instance.Publish(__instance, new ShipUnlockedUpgradePiecesChanged(__instance));

        return false;
    }

    // Runs from the Owner setter; clients keep the list the server sends.
    [HarmonyPatch(nameof(Ship.ResetUnlockedUpgradePieces))]
    [HarmonyPrefix]
    private static bool ResetUnlockedUpgradePiecesPrefix(Ship __instance)
    {
        if (ModInformation.IsClient) return false;

        var previous = __instance._unlockedUpgradePieces;
        __instance._unlockedUpgradePieces = IsPlayerOwned(__instance) ? CreateUnlockedPiecesFromEquipped(__instance) : null;

        if (previous == null && __instance._unlockedUpgradePieces == null) return false;
        if (CallOriginalPolicy.IsOriginalAllowed()) return false;

        MessageBroker.Instance.Publish(__instance, new ShipUnlockedUpgradePiecesChanged(__instance));
        return false;
    }

    private static bool IsPlayerOwned(Ship ship)
    {
        var party = ship.Owner?.MobileParty;
        return party != null && party.IsPlayerParty();
    }

    private static MBList<ShipUpgradePiece> CreateUnlockedPiecesFromEquipped(Ship ship)
    {
        var pieces = new MBList<ShipUpgradePiece>(ship._shipPieces.Count);
        foreach (var piece in ship._shipPieces.Values)
        {
            if (piece != null)
                pieces.Add(piece);
        }
        return pieces;
    }
}
