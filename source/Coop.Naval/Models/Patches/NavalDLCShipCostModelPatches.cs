using GameInterface.Services.Clans.Extensions;
using HarmonyLib;
using Helpers;
using NavalDLC;
using NavalDLC.CharacterDevelopment;
using NavalDLC.GameComponents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Coop.Naval.Models.Patches;

[HarmonyPatch(typeof(NavalDLCShipCostModel))]
internal class NavalDLCShipCostModelPatches
{
    // Trade passes the buyer and repair passes the owner as buyer, which is the clan vanilla checks
    [HarmonyPatch(nameof(NavalDLCShipCostModel.GetShipBaseValue))]
    [HarmonyPrefix]
    public static void GetShipBaseValuePrefix(ref bool applyAiDiscount, PartyBase buyer)
    {
        var buyerClan = buyer?.MobileParty?.ActualClan;

        // Don't apply the 99% discount for player clans
        if (applyAiDiscount && buyerClan.IsPlayerClan())
        {
            applyAiDiscount = false;
        }
    }

    [HarmonyPatch(nameof(NavalDLCShipCostModel.GetShipUpgradePieceValueInternal))]
    [HarmonyPrefix]
    public static bool GetShipUpgradePieceValueInternalPrefix(ref int __result, Ship ship, ShipUpgradePiece piece, PartyBase owner, PartyBase buyer)
    {
        float value = piece.LightValue;
        if (ship.ShipHull.Type == ShipHull.ShipType.Medium)
        {
            value = piece.MediumValue;
        }
        else if (ship.ShipHull.Type == ShipHull.ShipType.Heavy)
        {
            value = piece.HeavyValue;
        }

        var ownerClan = owner?.MobileParty?.ActualClan;
        if (owner != null)
        {
            if (owner.IsMobile)
            {
                var explainedValue = new ExplainedNumber(value);
                PerkHelper.AddPerkBonusForParty(NavalPerks.Boatswain.MasterShipwright, owner.MobileParty, true, ref explainedValue, false);
                value = explainedValue.ResultNumber;
            }

            var ownerKingdom = ownerClan?.Kingdom;
            if (ownerKingdom != null && ownerKingdom.RulingClan == ownerClan && ownerKingdom.HasPolicy(NavalPolicies.RoyalNavyPrerogative))
            {
                value *= 0.9f;
            }
        }

        // Replace Clan.PlayerClan usage so that the 99% AI discount isn't applied to player clans
        var buyerClan = buyer?.MobileParty?.ActualClan;
        if (!ownerClan.IsPlayerClan() && !buyerClan.IsPlayerClan())
        {
            value *= 0.01f;
        }

        __result = MathF.Round(value);
        return false;
    }
}
