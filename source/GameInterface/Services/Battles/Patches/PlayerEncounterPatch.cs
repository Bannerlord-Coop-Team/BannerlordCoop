using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Battles.Messages;
using HarmonyLib;
using Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;
using static TaleWorlds.CampaignSystem.MapEvents.MapEvent;
using static TaleWorlds.MountAndBlade.MovementOrder;

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

            //PartyBase.MainParty.Side = __instance.PlayerSide;
            //IS TWICE TRIGGER BAD?
            var message = new PlayerStartBattle();

            MessageBroker.Instance.Publish(__instance, message);

            return false;
        }
    }

    [HarmonyPatch(typeof(EncounterGameMenuBehavior))]
    public class PlayerEncounterPatch241
    {
        [HarmonyPatch(nameof(EncounterGameMenuBehavior.game_menu_town_besiege_continue_siege_on_condition))]
        [HarmonyPrefix]
        public static bool Prefix(ref bool __result, MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Continue;
            PartyBase encounteredParty = PlayerEncounter.EncounteredParty;
            if (encounteredParty == null)
            {
                __result = false;
                return false;
            }

            MapEvent encounteredBattle = PlayerEncounter.EncounteredBattle;

            _ = encounteredBattle.GetLeaderParty(PartyBase.MainParty.Side) == PartyBase.MainParty;
            _ = encounteredParty.IsSettlement;
            _ = encounteredParty.Settlement.IsFortification;
            _ = FactionManager.IsAtWarAgainstFaction(Hero.MainHero.MapFaction, encounteredParty.MapFaction);
            _ = encounteredParty.Settlement.IsUnderSiege;
            _ = encounteredParty.Settlement.CurrentSiegeState == Settlement.SiegeState.OnTheWalls;


            __result = encounteredBattle != null 
                && encounteredBattle.GetLeaderParty(PartyBase.MainParty.Side) == PartyBase.MainParty 
                && encounteredParty.IsSettlement 
                && encounteredParty.Settlement.IsFortification 
                && FactionManager.IsAtWarAgainstFaction(Hero.MainHero.MapFaction, encounteredParty.MapFaction) 
                && encounteredParty.Settlement.IsUnderSiege 
                && encounteredParty.Settlement.CurrentSiegeState == Settlement.SiegeState.OnTheWalls;

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