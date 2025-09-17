using Common.Logging;
using GameInterface.Policies;
using HarmonyLib;
using Helpers;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch(typeof(MapEvent))]
internal class MapEventDisablePatches
{
    [HarmonyPatch(nameof(MapEvent.Initialize))]
    [HarmonyPrefix]
    static bool InitializePrefix(MapEvent __instance, PartyBase attackerParty, PartyBase defenderParty, MapEventComponent component, MapEvent.BattleTypes mapEventType)
    {
        // Call original if we called it
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        __instance.Component = component;
        __instance.FirstUpdateIsDone = false;
        __instance.AttackersRanAway = false;
        __instance.MapEventSettlement = null;
        __instance._mapEventType = mapEventType;
        __instance._mapEventUpdateCount = 0;

        //Added - should probably be added elsewhere instead or make them work at creation
        __instance._sides = new MapEventSide[2];
        __instance.StrengthOfSide = new float[2];
        __instance.MapEventVisual = Campaign.Current.VisualCreator.CreateMapEventVisual(__instance);
        //

        __instance._sides[0] = new MapEventSide(__instance, BattleSideEnum.Defender, defenderParty);
        __instance._sides[1] = new MapEventSide(__instance, BattleSideEnum.Attacker, attackerParty);
        if (attackerParty.MobileParty == MobileParty.MainParty || defenderParty.MobileParty == MobileParty.MainParty)
        {
            if (mapEventType == MapEvent.BattleTypes.Raid)
            {
            }
            else if (defenderParty.IsSettlement && defenderParty.Settlement.IsFortification)
            {
            }
        }
        if (attackerParty.IsMobile && attackerParty.MobileParty.CurrentSettlement != null)
        {
            __instance.MapEventSettlement = attackerParty.MobileParty.CurrentSettlement;
        }
        else if (defenderParty.IsMobile && defenderParty.MobileParty.CurrentSettlement != null)
        {
            __instance.MapEventSettlement = defenderParty.MobileParty.CurrentSettlement;
        }
        else if ((!attackerParty.IsMobile || attackerParty.MobileParty.BesiegedSettlement == null) && defenderParty.IsMobile)
        {
            Settlement besiegedSettlement = defenderParty.MobileParty.BesiegedSettlement;
        }
        if (attackerParty.IsSettlement)
        {
            __instance.MapEventSettlement = attackerParty.Settlement;
        }
        else if (defenderParty.IsSettlement)
        {
            __instance.MapEventSettlement = defenderParty.Settlement;
            __instance.MapEventSettlement.LastAttackerParty = attackerParty.MobileParty;
        }
        if (__instance.IsFieldBattle)
        {
            __instance.MapEventSettlement = null;
            if (attackerParty == PartyBase.MainParty || defenderParty == PartyBase.MainParty)
            {
                Settlement settlement = SettlementHelper.FindNearestVillage((Settlement x) => x.Position2D.DistanceSquared(attackerParty.Position2D) < 9f, null);
                if (settlement != null)
                {
                    __instance.MapEventSettlement = settlement;
                }
            }
        }
        __instance.Position = __instance.CalculateMapEventPosition(attackerParty, defenderParty);
        __instance._eventTerrainType = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(Campaign.Current.MapSceneWrapper.GetFaceIndex(__instance.Position));
        __instance.CacheSimulationData();
        attackerParty.MapEventSide = __instance.AttackerSide;
        defenderParty.MapEventSide = __instance.DefenderSide;
        if (__instance.MapEventSettlement != null && (mapEventType == MapEvent.BattleTypes.Siege || mapEventType == MapEvent.BattleTypes.SiegeOutside || mapEventType == MapEvent.BattleTypes.SallyOut || __instance.IsSiegeAmbush))
        {
            foreach (PartyBase partyBase in __instance.MapEventSettlement.SiegeEvent.BesiegerCamp.GetInvolvedPartiesForEventType(mapEventType))
            {
                if (partyBase.MapEventSide == null && (partyBase != PartyBase.MainParty || partyBase.MobileParty.Army != null) && (partyBase.MobileParty.Army == null || partyBase.MobileParty.Army.LeaderParty == partyBase.MobileParty))
                {
                    partyBase.MapEventSide = ((mapEventType == MapEvent.BattleTypes.SallyOut) ? defenderParty.MapEventSide : attackerParty.MapEventSide);
                }
            }
        }
        if (defenderParty.IsMobile && defenderParty.MobileParty.BesiegedSettlement != null)
        {
            List<PartyBase> involvedPartiesForEventType = defenderParty.MobileParty.SiegeEvent.GetInvolvedPartiesForEventType(__instance._mapEventType);
            PartyBase partyBase2 = __instance.IsSiegeAssault ? attackerParty : defenderParty;
            foreach (PartyBase partyBase3 in involvedPartiesForEventType)
            {
                if (partyBase3 != partyBase2 && partyBase3.IsMobile && partyBase3 != PartyBase.MainParty && partyBase3.MobileParty.BesiegedSettlement == defenderParty.MobileParty.BesiegedSettlement && (partyBase3.MobileParty.Army == null || partyBase3.MobileParty.Army.LeaderParty == partyBase3.MobileParty))
                {
                    partyBase3.MapEventSide = __instance.DefenderSide;
                }
            }
        }
        __instance.State = MapEventState.Wait;
        __instance._mapEventStartTime = CampaignTime.Now;
        __instance._nextSimulationTime = MapEvent.CalculateNextSimulationTime();
        MapEventComponent component2 = __instance.Component;
        if (component2 != null)
        {
            component2.InitializeComponent();
        }
        if (__instance.MapEventSettlement != null)
        {
            __instance.AddInsideSettlementParties(__instance.MapEventSettlement);
        }
        __instance.MapEventVisual.Initialize(__instance.Position, __instance.GetBattleSizeValue(), __instance.AttackerSide.LeaderParty != PartyBase.MainParty && __instance.DefenderSide.LeaderParty != PartyBase.MainParty, __instance.IsVisible);
        __instance.BattleState = BattleState.None;
        CampaignEventDispatcher.Instance.OnMapEventStarted(__instance, attackerParty, defenderParty);

        return false;
    }
}
