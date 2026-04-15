using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using Helpers;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEvents.Patches
{
    [HarmonyPatch(typeof(MapEvent))]
    public class MapEventUpdatePatch
    {
        [HarmonyPrefix]
        [HarmonyPatch("Update")]
        static bool PrefixUpdate(MapEvent __instance)
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient) return false;

            // Don't update if a player is involved
            // Prevents server from instantly finishing the battle and waits for client finish request
            if (__instance.InvolvedParties.Any(x => x.MobileParty.IsPartyControlled() == false)) return false;

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(MapEvent.AddInvolvedPartyInternal))]
        static bool Prefix(MapEvent __instance, MapEventParty mapEventParty, BattleSideEnum side)
        {
            if (mapEventParty.Party == PartyBase.MainParty)
            {
                __instance.TroopUpgradeTracker = new TroopUpgradeTracker();
                MapEventSide[] sides = __instance._sides;
                for (int i = 0; i < sides.Length; i++)
                {
                    foreach (MapEventParty mapEventParty2 in sides[i].Parties)
                    {
                        __instance.TroopUpgradeTracker.AddParty(mapEventParty2);
                    }
                }
            }
            else
            {
                TroopUpgradeTracker troopUpgradeTracker = __instance.TroopUpgradeTracker;
                if (troopUpgradeTracker != null)
                {
                    troopUpgradeTracker.AddParty(mapEventParty);
                }
            }
            PartyBase party = mapEventParty.Party;
            if (__instance.IsSiegeAssault && party.MobileParty != null && party.MobileParty.CurrentSettlement == null && side == BattleSideEnum.Defender)
            {
                __instance._mapEventType = MapEvent.BattleTypes.SiegeOutside;
            }
            if (party.MobileParty != null && party.MobileParty.IsGarrison && side == BattleSideEnum.Attacker && (__instance.IsSiegeOutside || __instance.IsBlockade))
            {
                __instance._mapEventType = (__instance.IsSiegeOutside ? MapEvent.BattleTypes.SallyOut : MapEvent.BattleTypes.BlockadeSallyOutBattle);
                __instance.MapEventSettlement = party.MobileParty.CurrentSettlement;
            }
            if (party == MobileParty.MainParty.Party && !__instance.IsSiegeAssault && !__instance.IsRaid)
            {
                party.MobileParty.SetMoveModeHold();
            }
            if (party == PartyBase.MainParty)
            {
                party.MobileParty.ForceAiNoPathMode = false;
            }
            __instance.RecalculateRenownAndInfluenceValues(party);
            if (__instance.IsFieldBattle && party.IsMobile && party.MobileParty.BesiegedSettlement == null)
            {
                int sideIndex = __instance.GetMapEventSide(side).Parties.Count((MapEventParty p) => p.Party.IsMobile) - 1;
                __instance.SetPartyBaseEventLocalPosition(party, side, sideIndex);
            }
            party.SetVisualAsDirty();
            if (party.IsMobile && party.MobileParty.Army != null && party.MobileParty.Army.LeaderParty == party.MobileParty)
            {
                foreach (MobileParty mobileParty in party.MobileParty.Army.LeaderParty.AttachedParties)
                {
                    mobileParty.Party.SetVisualAsDirty();
                }
            }
            if (__instance.HasWinner && party.MapEventSide.MissionSide != __instance.WinningSide && party.NumberOfHealthyMembers > 0)
            {
                __instance.BattleState = BattleState.None;
            }
            if (party.IsVisible)
            {
                __instance.IsVisible = true;
            }
            __instance.ResetUnsuitablePartiesThatWereTargetingThisMapEvent();
            MapEventComponent component = __instance.Component;
            if (component != null)
            {
                component.OnPartyAdded(party);
            }
            CampaignEventDispatcher.Instance.OnPartyAddedToMapEvent(party);

            return false;
        }
    }

    [HarmonyPatch(typeof(MapEvent))]
    public class MapEventUpdatePatch2
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(MapEvent.RecalculateRenownAndInfluenceValues))]
        static bool PrefixUpdate(MapEvent __instance, PartyBase party)
        {
            __instance.StrengthOfSide[(int)party.Side] += party.GetCustomStrength(party.Side, __instance.SimulationContext);
            MapEventSide[] sides = __instance._sides;
            for (int i = 0; i < sides.Length; i++)
            {
                if (sides[i] == null) continue;
                sides[i].CalculateRenownAndInfluenceValues(__instance.StrengthOfSide);
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(MapEvent))]
    public class MapEventInitPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(MapEvent.Initialize))]
        static bool Prefix(MapEvent __instance, PartyBase attackerParty, PartyBase defenderParty, MapEventComponent component, MapEvent.BattleTypes mapEventType)
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient) return false;

            MapEventInitialize message = new MapEventInitialize(__instance, mapEventType, attackerParty, defenderParty);

            MessageBroker.Instance.Publish(__instance, message);

            return true;
        }
    }

    [HarmonyPatch(typeof(BattleSimulation))]
    public class BattleSimulationUpdatePatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(BattleSimulation.SimulateBattle))]
        static bool PrefixUpdate(BattleSimulation __instance)
        {
            return false;
        }
    }
}