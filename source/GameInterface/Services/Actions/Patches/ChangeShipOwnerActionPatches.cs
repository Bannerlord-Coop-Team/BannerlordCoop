using Common;
using GameInterface.Services.PartyBases.Extensions;
using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Actions.Patches;

[HarmonyPatch(typeof(ChangeShipOwnerAction))]
[HarmonyPatchCategory(GameInterface.HARMONY_NAVAL_CATEGORY)]
internal class ChangeShipOwnerActionPatches
{
    [HarmonyPatch(nameof(ChangeShipOwnerAction.ApplyInternal))]
    [HarmonyPrefix]
    public static bool ApplyInternalPrefix(PartyBase newOwner, Ship ship, ChangeShipOwnerAction.ShipOwnerChangeDetail changeDetail)
    {
        if (ModInformation.IsClient) return true;

        if (changeDetail != ChangeShipOwnerAction.ShipOwnerChangeDetail.ApplyByTrade) return true;

        PartyBase owner = ship.Owner;

        // Vanilla trades the gold with the clan leader, which is only the player when they lead their clan
        if (owner.IsSettlement && newOwner.IsPlayerParty())
        {
            int tradeValue = (int)Campaign.Current.Models.ShipCostModel.GetShipTradeValue(ship, owner, newOwner);
            GiveGoldAction.ApplyBetweenCharacters(newOwner.LeaderHero, null, tradeValue);
            if (newOwner.Ships.Any() && !newOwner.MobileParty.Anchor.IsValid)
            {
                newOwner.MobileParty.Anchor.Settlement = owner.Settlement;
            }
        }
        else if (owner.IsPlayerParty())
        {
            int tradeValue = (int)Campaign.Current.Models.ShipCostModel.GetShipTradeValue(ship, owner, newOwner);
            GiveGoldAction.ApplyBetweenCharacters(null, owner.LeaderHero, tradeValue);
        }
        else
        {
            return true;
        }

        // Same as the end of vanilla ChangeShipOwnerAction.ApplyInternal from here
        ship.Owner = newOwner;
        owner.MobileParty?.SetNavalVisualAsDirty();
        newOwner.MobileParty?.SetNavalVisualAsDirty();

        bool canHaveUpgradePiece = false;
        CampaignEventDispatcher.Instance.CanHaveUnlockedUpgradePiece(ship, changeDetail, ref canHaveUpgradePiece);
        if (ship.CanHaveUnlockedPieces != canHaveUpgradePiece)
        {
            ship.CanHaveUnlockedPieces = canHaveUpgradePiece;
        }

        CampaignEventDispatcher.Instance.OnShipOwnerChanged(ship, owner, changeDetail);

        return false;
    }
}
