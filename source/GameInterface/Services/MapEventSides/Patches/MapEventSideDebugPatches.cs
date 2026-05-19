using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.LinQuick;
using static TaleWorlds.CampaignSystem.MapEvents.MapEvent;

namespace GameInterface.Services.MapEventSides.Patches;

//[HarmonyPatch(typeof(DefaultMilitaryPowerModel))]
//public class Debugsss
//{
//    [HarmonyPatch()]
//    private static bool Test(DefaultMilitaryPowerModel __instance, CampaignVec2 position, PowerCalculationContext __result)
//    {
//        TerrainType terrainTypeAtPosition = Campaign.Current.MapSceneWrapper.GetTerrainTypeAtPosition(position);
//        if (position.IsOnLand)
//        {
//            MapWeatherModel.WeatherEvent weatherEventInPosition = Campaign.Current.Models.MapWeatherModel.GetWeatherEventInPosition(position.ToVec2());
//            if (weatherEventInPosition == MapWeatherModel.WeatherEvent.Snowy || weatherEventInPosition == MapWeatherModel.WeatherEvent.Blizzard)
//            {
//                __result = MapEvent.PowerCalculationContext.SnowBattle;
//                return false;
//            }
//        }
//        switch (terrainTypeAtPosition)
//        {
//            case TerrainType.Plain:
//                __result = MapEvent.PowerCalculationContext.PlainBattle;
//                return false;
//            case TerrainType.Desert:
//                __result = MapEvent.PowerCalculationContext.DesertBattle;
//                return false;
//            case TerrainType.Snow:
//                __result = MapEvent.PowerCalculationContext.SnowBattle;
//                return false;
//            case TerrainType.Forest:
//                __result = MapEvent.PowerCalculationContext.ForestBattle;
//                return false;
//            case TerrainType.Steppe:
//                __result = MapEvent.PowerCalculationContext.SteppeBattle;
//                return false;
//            case TerrainType.Fording:
//                if (!position.IsOnLand)
//                {
//                    __result = MapEvent.PowerCalculationContext.RiverCrossingBattle;
//                    return false;
//                }
//                __result = MapEvent.PowerCalculationContext.PlainBattle;
//                return false;
//            case TerrainType.Lake:
//                __result = MapEvent.PowerCalculationContext.RiverCrossingBattle;
//                return false;
//            case TerrainType.Water:
//                __result = MapEvent.PowerCalculationContext.SeaBattle;
//                return false;
//            case TerrainType.River:
//                __result = MapEvent.PowerCalculationContext.RiverCrossingBattle;
//                return false;
//            case TerrainType.Swamp:
//                __result = MapEvent.PowerCalculationContext.PlainBattle;
//                return false;
//            case TerrainType.Dune:
//                __result = MapEvent.PowerCalculationContext.DuneBattle;
//                return false;
//            case TerrainType.Bridge:
//                __result = MapEvent.PowerCalculationContext.PlainBattle;
//                return false;
//            case TerrainType.CoastalSea:
//                __result = MapEvent.PowerCalculationContext.SeaBattle;
//                return false;
//            case TerrainType.OpenSea:
//                __result = MapEvent.PowerCalculationContext.OpenSeaBattle;
//                return false;
//            case TerrainType.UnderBridge:
//                __result = MapEvent.PowerCalculationContext.RiverCrossingBattle;
//                return false;
//        }
//        __result = MapEvent.PowerCalculationContext.PlainBattle;
//        return false;
//    }
//}

//[HarmonyPatch(typeof(MapEventSide))]
//internal class MapEventSideDebugPatches
//{
//    [HarmonyPrefix]
//    [HarmonyPatch(nameof(MapEventSide.HandleMapEventEnd))]
//    static bool Prefix_HandleMapEventEnd(ref MapEventSide __instance)
//    {
//        while (__instance.Parties.Count > 0)
//        {
//            MapEventParty mapEventParty = __instance.Parties.FirstOrDefault((MapEventParty x) => !x.Party.IsMobile || x.Party.MobileParty.Army == null || x.Party.MobileParty.Army.LeaderParty != x.Party.MobileParty) ?? __instance.Parties[__instance.Parties.Count - 1];
//            __instance.HandleMapEventEndForPartyInternal(mapEventParty.Party);
//        }

//        return false;
//    }

//    [HarmonyPrefix]
//    [HarmonyPatch(nameof(MapEventSide.HandleMapEventEndForPartyInternal))]
//    static bool Prefix_HandleMapEventEndForPartyInternal(ref MapEventSide __instance, PartyBase party)
//    {
//        IEnumerable<TroopRosterElement> enumerable = party.MemberRoster.GetTroopRoster().WhereQ((TroopRosterElement x) => x.Character.IsHero && x.Character.HeroObject.IsAlive && x.Character.HeroObject.DeathMark == KillCharacterAction.KillCharacterActionDetail.DiedInBattle);
//        PartyBase leaderParty = __instance._mapEvent.GetLeaderParty(party.OpponentSide);
//        bool flag = __instance._mapEvent.IsWinnerSide(party.Side);
//        party.MapEventSide = null;
//        foreach (TroopRosterElement troopRosterElement in enumerable)
//        {
//            KillCharacterAction.ApplyByBattle(troopRosterElement.Character.HeroObject, __instance.OtherSide.LeaderParty.LeaderHero, true);
//        }
//        if (party.IsMobile && party != PartyBase.MainParty && party.IsActive && (party.NumberOfAllMembers == 0 || (!flag && !__instance.MapEvent.EndedByRetreat && (party.NumberOfHealthyMembers == 0 || (__instance._mapEvent.BattleState != BattleState.None && party.MobileParty.IsMilitia)) && (party.MobileParty.Army == null || party.MobileParty.Army.LeaderParty.Party.NumberOfHealthyMembers == 0))) && (!party.MobileParty.IsDisbanding || party.MemberRoster.Count == 0))
//        {
//            if (party.LeaderHero != null)
//            {
//                party.LeaderHero.ChangeState(Hero.CharacterStates.Fugitive);
//            }
//            DestroyPartyAction.Apply(leaderParty, party.MobileParty);
//        }
//        party.MemberRoster.RemoveZeroCounts();
//        party.PrisonRoster.RemoveZeroCounts();
//        if (party.IsMobile && party.MobileParty.IsActive && party.MobileParty.CurrentSettlement == null)
//        {
//            party.SetVisualAsDirty();
//        }

//        return false;
//    }
//}
