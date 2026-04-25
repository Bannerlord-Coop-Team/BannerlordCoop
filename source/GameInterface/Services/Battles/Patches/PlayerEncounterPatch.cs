using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Battles.Messages;
using HarmonyLib;
using Helpers;
using Serilog;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
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
    //[HarmonyPatch(typeof(PartyBase))]
    //public class PlayerEncounterPatch151
    //{
    //    [HarmonyPatch(nameof(PartyBase.MapEventSide), MethodType.Setter)]
    //    [HarmonyPrefix]
    //    public static bool Prefix(ref PartyBase __instance, ref MapEventSide value)
    //    {
    //        if (__instance._mapEventSide != value)
    //        {
    //            if (value != null && __instance.IsMobile && __instance.MapEvent != null && __instance.MapEvent.DefenderSide.LeaderParty == __instance)
    //            {
    //                Debug.FailedAssert(string.Format("Double MapEvent For {0}", __instance.Name), "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Party\\PartyBase.cs", "MapEventSide", 257);
    //            }
    //            if (__instance._mapEventSide != null)
    //            {
    //                __instance._mapEventSide.RemovePartyInternal(__instance);
    //            }
    //            __instance._mapEventSide = value;
    //            if (__instance._mapEventSide != null)
    //            {
    //                __instance._mapEventSide.AddPartyInternal(__instance);
    //            }
    //            if (__instance.MobileParty != null)
    //            {
    //                if (__instance.IsActive)
    //                {
    //                    __instance.MobileParty.CancelNavigationTransition();
    //                }
    //                foreach (MobileParty mobileParty in __instance.MobileParty.AttachedParties)
    //                {
    //                    mobileParty.Party.MapEventSide = __instance._mapEventSide;
    //                }
    //            }
    //        }

    //        return false;
    //    }
    //}

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
        static readonly ILogger Logger = LogManager.GetLogger<StartBattleActionPatchFix>();

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

                if (defenderParty.MapEvent._sides.Any(side => side.Parties is null))
                {
                    Logger.Error($"Mapevent side did not have a party.");
                    return false;
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