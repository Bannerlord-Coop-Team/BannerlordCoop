using GameInterface.Services;
using Helpers;
using NavalDLC.CharacterDevelopment;
using NavalDLC.GameComponents;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Coop.Naval.Models.Interfaces;

public interface INavalDLCTroopSacrificeModelInterface : IGameAbstraction
{
    void GetShipsToSacrificeForTryingToGetAway(MobileParty mainParty, BattleSideEnum playerBattleSide, MapEvent mapEvent, out MBList<Ship> shipsToCapture, out Ship shipToTakeDamage, out float damageToApplyForLastShip);
}

public class NavalDLCTroopSacrificeModelInterface : INavalDLCTroopSacrificeModelInterface
{
    public void GetShipsToSacrificeForTryingToGetAway(MobileParty mainParty, BattleSideEnum playerBattleSide, MapEvent mapEvent, out MBList<Ship> shipsToCapture, out Ship shipToTakeDamage, out float damageToApplyForLastShip)
    {
        damageToApplyForLastShip = float.MinValue;
        shipsToCapture = new MBList<Ship>();
        shipToTakeDamage = null;
        mapEvent.RecalculateStrengthOfSides();
        List<Ship> ships = mapEvent.PartiesOnSide(playerBattleSide).SelectMany(party => party.Ships).ToList();
        if (ships.Count == 0) return;

        if (CanPlayerSideTryToGetAwayWithTheirShipStats(mainParty, playerBattleSide, mapEvent, out var totalDamageToApply))
        {
            float maxHitPoints = ships.MaxBy(ship => ship.MaxHitPoints).MaxHitPoints;
            if (totalDamageToApply <= ships.MinBy(ship => ship.HitPoints).HitPoints)
            {
                shipsToCapture.Add(ships.MinBy(ship => ship.HitPoints));
                return;
            }
            while (totalDamageToApply > 0f)
            {
                Ship shipToSacrifice = NavalDLCTroopSacrificeModel.GetShipToSacrifice(maxHitPoints, ships);
                if (totalDamageToApply < shipToSacrifice.HitPoints)
                {
                    shipToTakeDamage = shipToSacrifice;
                    damageToApplyForLastShip = totalDamageToApply;
                    break;
                }
                shipsToCapture.Add(shipToSacrifice);
                totalDamageToApply -= shipToSacrifice.HitPoints;
                ships.Remove(shipToSacrifice);
            }
        }
    }

    // Replace PlayerEncounter and MobileParty.MainParty usage with the fleeing party's map event and party
    private static bool CanPlayerSideTryToGetAwayWithTheirShipStats(MobileParty mainParty, BattleSideEnum playerSide, MapEvent battle, out float totalDamageToApply)
    {
        float playerSideHitPoints = battle.PartiesOnSide(playerSide).Sum(party => party.Ships.Sum(ship => ship.HitPoints));
        float enemySideHitPoints = battle.PartiesOnSide(playerSide.GetOppositeSide()).Sum(party => party.Ships.Sum(ship => ship.HitPoints));
        totalDamageToApply = playerSideHitPoints * MathF.Pow(MathF.Min(enemySideHitPoints / playerSideHitPoints, 3f), 1.3f) * 0.1f;
        if (totalDamageToApply > 0f)
        {
            ExplainedNumber damage = new(totalDamageToApply);
            SkillHelper.AddSkillBonusForParty(NavalSkillEffects.ShipDamageReduction, mainParty, ref damage);
            float result = damage.ResultNumber;
            if (mainParty.HasPerk(NavalPerks.Shipmaster.GhostShip))
            {
                result -= result * 0.5f;
            }
            ExplainedNumber mainPartySpeed = Campaign.Current.Models.PartySpeedCalculatingModel.CalculateBaseSpeed(mainParty);
            ExplainedNumber enemySpeed = Campaign.Current.Models.PartySpeedCalculatingModel.CalculateBaseSpeed(battle.GetLeaderParty(playerSide.GetOppositeSide()).MobileParty);
            if (mainPartySpeed.ResultNumber > enemySpeed.ResultNumber)
            {
                result -= result * MBMath.ClampFloat(mainPartySpeed.ResultNumber / enemySpeed.ResultNumber, 1f, 5f) * 0.1f;
            }
            totalDamageToApply = result;
        }
        return totalDamageToApply < playerSideHitPoints;
    }
}
