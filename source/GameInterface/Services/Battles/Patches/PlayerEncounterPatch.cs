using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Battles.Messages;
using HarmonyLib;
using Helpers;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.MobileParties.Patches
{
    /// <summary>
    /// Patches the StartBattle in PlayerEncounter, only runs on local client
    /// </summary>
    [HarmonyPatch(typeof(PlayerEncounter))]
    public class PlayerEncounterPatch
    {
        [HarmonyPatch("StartBattleInternal")]
        [HarmonyPrefix]
        public static bool Prefix(ref PlayerEncounter __instance)
        {
            if (AllowedThread.IsThisThreadAllowed()) return true;

            if (ModInformation.IsServer) return true;

            using(new AllowedThread())
            {
                PlayerEncounter.Current.StartBattleInternal();
            }

            return false;
        }
    }

    //It crashes without this, WTF!? it changes NOTHING!?
    [HarmonyPatch(typeof(PartyBase))]
    public class PlayerEncounterPatch151
    {
        [HarmonyPatch(nameof(PartyBase.MapEventSide), MethodType.Setter)]
        [HarmonyPrefix]
        public static bool Prefix(ref PartyBase __instance, ref MapEventSide value)
        {
            if (__instance._mapEventSide != value)
            {
                if (value != null && __instance.IsMobile && __instance.MapEvent != null && __instance.MapEvent.DefenderSide.LeaderParty == __instance)
                {
                    Debug.FailedAssert(string.Format("Double MapEvent For {0}", __instance.Name), "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Party\\PartyBase.cs", "MapEventSide", 257);
                }
                if (__instance._mapEventSide != null)
                {
                    __instance._mapEventSide.RemovePartyInternal(__instance);
                }
                __instance._mapEventSide = value;
                if (__instance._mapEventSide != null)
                {
                    __instance._mapEventSide.AddPartyInternal(__instance);
                }
                if (__instance.MobileParty != null)
                {
                    if (__instance.IsActive)
                    {
                        __instance.MobileParty.CancelNavigationTransition();
                    }
                    foreach (MobileParty mobileParty in __instance.MobileParty.AttachedParties)
                    {
                        mobileParty.Party.MapEventSide = __instance._mapEventSide;
                    }
                }
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(MapEvent))]
    public class MapEventPatch1241
    {
        [HarmonyPatch(nameof(MapEvent.Initialize))]
        [HarmonyPrefix]
        public static bool Prefix(ref MapEvent __instance, PartyBase attackerParty, PartyBase defenderParty, MapEventComponent component = null, MapEvent.BattleTypes mapEventType = MapEvent.BattleTypes.None)
        {
            __instance.Component = component;
            __instance.FirstUpdateIsDone = false;
            __instance.RetreatingSide = BattleSideEnum.None;
            __instance.PursuitRoundNumber = 0;
            __instance.MapEventSettlement = null;
            __instance._mapEventType = mapEventType;
            __instance._sides[0] = new MapEventSide(__instance, BattleSideEnum.Defender, defenderParty);
            __instance._sides[1] = new MapEventSide(__instance, BattleSideEnum.Attacker, attackerParty);
            if (attackerParty.MobileParty == MobileParty.MainParty || defenderParty.MobileParty == MobileParty.MainParty)
            {
                if (mapEventType == MapEvent.BattleTypes.Raid)
                {
                    Debug.Print("A raid mapEvent has been started on " + defenderParty.Name + "\n", 0, Debug.DebugColor.DarkGreen, 64UL);
                }
                else if (defenderParty.IsSettlement && defenderParty.Settlement.IsFortification)
                {
                    Debug.Print("A siege mapEvent has been started on " + defenderParty.Name + "\n", 0, Debug.DebugColor.DarkCyan, 64UL);
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
                if (!__instance.IsNavalMapEvent && (attackerParty == PartyBase.MainParty || defenderParty == PartyBase.MainParty))
                {
                    float settlementBeingNearFieldBattleRadius = Campaign.Current.Models.EncounterModel.GetSettlementBeingNearFieldBattleRadius;
                    Village village = SettlementHelper.FindNearestVillageToMobileParty(MobileParty.MainParty, MobileParty.NavigationType.Default, (Settlement x) => x.Position.DistanceSquared(attackerParty.Position) < settlementBeingNearFieldBattleRadius * settlementBeingNearFieldBattleRadius);
                    if (village != null)
                    {
                        __instance.MapEventSettlement = village.Settlement;
                        float num;
                        if (Campaign.Current.Models.MapDistanceModel.GetDistance(attackerParty.MobileParty, __instance.MapEventSettlement, false, MobileParty.NavigationType.Default, out num) > settlementBeingNearFieldBattleRadius * 1.5f || Campaign.Current.Models.MapDistanceModel.GetDistance(defenderParty.MobileParty, __instance.MapEventSettlement, false, MobileParty.NavigationType.Default, out num) > settlementBeingNearFieldBattleRadius * 1.5f)
                        {
                            __instance.MapEventSettlement = null;
                        }
                    }
                }
            }
            if (__instance.IsBlockade || __instance.IsBlockadeSallyOut)
            {
                __instance.Position = defenderParty.MobileParty.BesiegedSettlement.PortPosition;
                __instance.MapEventSettlement = defenderParty.MobileParty.BesiegedSettlement;
            }
            else
            {
                __instance.Position = attackerParty.Position;
            }
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
            if (__instance.MapEventSettlement != null && !__instance.IsBlockade)
            {
                __instance.AddInsideSettlementParties(__instance.MapEventSettlement);
            }
            MapEventComponent component2 = __instance.Component;
            if (component2 != null)
            {
                component2.InitializeComponent();
            }
            __instance.MapEventVisual.Initialize(__instance.Position, __instance.GetBattleSizeValue(), __instance.IsVisible);
            __instance.BattleState = BattleState.None;
            __instance.CacheSimulationLeaderModifiers();
            CampaignEventDispatcher.Instance.OnMapEventStarted(__instance, attackerParty, defenderParty);

            return false;
        }
    }

    [HarmonyPatch(typeof(MapEventHelper))]
    public class PlayerEncounterPatch241
    {
        [HarmonyPatch(nameof(MapEventHelper.CanMainPartyLeaveBattleCommonCondition))]
        [HarmonyPrefix]
        public static bool Prefix(ref bool __result)
        {
            if (MobileParty.MainParty.MapEvent.PlayerSide != BattleSideEnum.Defender)
            {
                __result = true;
                return false;
            }
                
            __result = MobileParty.MainParty.SiegeEvent != null && !MobileParty.MainParty.SiegeEvent.BesiegerCamp.IsBesiegerSideParty(MobileParty.MainParty) && MobileParty.MainParty.CurrentSettlement == null;
            return false;
        }
    }

    [HarmonyPatch(typeof(PartyBase))]
    public class TestPatching2
    {
        [HarmonyPatch("TaleWorlds.CampaignSystem.Map.IInteractablePoint.OnPartyInteraction")]
        [HarmonyPrefix]
        public static bool Prefix(PartyBase __instance, MobileParty engagingParty)
        {
            if (AllowedThread.IsThisThreadAllowed()) return true;

            if (ModInformation.IsClient) return false;

            var message = new BattleStarted(engagingParty, __instance);

            if (engagingParty.ActualClan != null && engagingParty.ActualClan.Name.ToString() == "Playerland")
            {
                InformationManager.DisplayMessage(new InformationMessage($"Local player is engaging in battle with {__instance.Name}"));
            }

            MessageBroker.Instance.Publish(__instance, message);

            return true;
        }
    }


    [HarmonyPatch(typeof(StartBattleAction))]
    public class StartBattleActionPatchFix
    {
        [HarmonyPatch(nameof(StartBattleAction.ApplyInternal))]
        [HarmonyPrefix]
        public static bool Prefix(PartyBase attackerParty, PartyBase defenderParty, object subject, MapEvent.BattleTypes battleType)
        {
            if(attackerParty.MobileParty == MobileParty.MainParty)
            {
                ;
            }
            if (defenderParty.MapEvent == null)
            {
                Campaign.Current.Models.EncounterModel.CreateMapEventComponentForEncounter(attackerParty, defenderParty, battleType);
                if (defenderParty.MapEvent == null)
                {
                    return false;
                }
            }
            else
            {
                BattleSideEnum side = BattleSideEnum.Attacker;
                if (defenderParty.Side == BattleSideEnum.Attacker)
                {
                    side = BattleSideEnum.Defender;
                }

                // A temporary fix that prevents a crash when the MapEvent has no involved parties, not sure why this happens
                if (defenderParty.MapEvent.InvolvedParties.Count() == 0)
                {
                    InformationManager.DisplayMessage(new InformationMessage($"Defender party {defenderParty.Name} has no involved parties in its map event. {SettlementHelper.FindNearestSettlementToMobileParty(defenderParty.MobileParty, MobileParty.NavigationType.Default).Name}"));
                    return false;
                }

                attackerParty.MapEventSide = defenderParty.MapEvent.GetMapEventSide(side);
            }
            if (defenderParty.MapEvent.IsPlayerMapEvent && !defenderParty.MapEvent.IsSallyOut && PlayerEncounter.Current != null && MobileParty.MainParty.CurrentSettlement != null)
            {
                PlayerEncounter.Current.InterruptEncounter("encounter_interrupted");
            }
            MobileParty mobileParty = attackerParty.MobileParty;
            bool flag;
            if (((mobileParty != null) ? mobileParty.Army : null) != null)
            {
                MobileParty mobileParty2 = attackerParty.MobileParty;
                if (((mobileParty2 != null) ? mobileParty2.Army.LeaderParty : null) != attackerParty.MobileParty)
                {
                    flag = false;
                    goto IL_F0;
                }
            }
            MobileParty mobileParty3 = defenderParty.MobileParty;
            if (((mobileParty3 != null) ? mobileParty3.Army : null) != null)
            {
                MobileParty mobileParty4 = defenderParty.MobileParty;
                flag = (((mobileParty4 != null) ? mobileParty4.Army.LeaderParty : null) == defenderParty.MobileParty);
            }
            else
            {
                flag = true;
            }
        IL_F0:
            bool flag2 = flag;
            if (flag2 && defenderParty.IsSettlement && defenderParty.MapEvent != null && defenderParty.MapEvent.DefenderSide.Parties.Count > 1)
            {
                flag2 = false;
            }
            CampaignEventDispatcher.Instance.OnStartBattle(attackerParty, defenderParty, subject, flag2);

            return false;
        }
    }
}