using Common;
using GameInterface.Policies;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch(typeof(MapEvent))]
public class MapEventUpdatePatch
{
    [HarmonyPrefix]
    [HarmonyPatch("Update")]
    static bool PrefixUpdate(MapEvent __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient) return false;

        // Skip if any parties are not set
        if (__instance.InvolvedParties.Any(x => x?.MobileParty is null)) return false;

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

    [HarmonyPrefix]
    [HarmonyPatch(nameof(MapEvent.Initialize))]
    static bool PrefixInitialize(MapEvent __instance, PartyBase attackerParty, PartyBase defenderParty, MapEventComponent component, MapEvent.BattleTypes mapEventType)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient) return false;

        //MapEventInitialize message = new MapEventInitialize(__instance, mapEventType, attackerParty, defenderParty);

        //MessageBroker.Instance.Publish(__instance, message);

        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(MapEvent.FinalizeEvent))]
    static bool PrefixFinalizeEvent(MapEvent __instance)
    {
        if (__instance.IsFinalized)
        {
            return false;
        }
        __instance.State = MapEventState.WaitingRemoval;
        CampaignEventDispatcher.Instance.OnMapEventEnded(__instance);
        bool isWin = false;
        bool flag = false;
        if (__instance.MapEventSettlement != null)
        {
            if (__instance.BattleState != BattleState.None && (__instance.IsSiegeAssault || __instance.IsSiegeOutside || __instance.IsSallyOut || __instance.IsBlockadeSallyOut || __instance.IsBlockade) && __instance.MapEventSettlement.SiegeEvent != null)
            {
                __instance.MapEventSettlement.SiegeEvent.OnBeforeSiegeEventEnd(__instance.BattleState, __instance._mapEventType);
            }
            if (!__instance._keepSiegeEvent && (__instance.IsSiegeAssault || __instance.IsSiegeOutside))
            {
                BattleState battleState = __instance.BattleState;
                if (battleState != BattleState.DefenderVictory)
                {
                    if (battleState == BattleState.AttackerVictory)
                    {
                        CampaignEventDispatcher.Instance.SiegeCompleted(__instance.MapEventSettlement, __instance.AttackerSide.LeaderParty.MobileParty, true, __instance._mapEventType);
                        isWin = true;
                    }
                }
                else
                {
                    SiegeEvent siegeEvent = __instance.MapEventSettlement.SiegeEvent;
                    if (siegeEvent != null)
                    {
                        siegeEvent.BesiegerCamp.RemoveAllSiegeParties();
                    }
                    CampaignEventDispatcher.Instance.SiegeCompleted(__instance.MapEventSettlement, __instance.AttackerSide.LeaderParty.MobileParty, false, __instance._mapEventType);
                }
                if (__instance.BattleState == BattleState.AttackerVictory || __instance.BattleState == BattleState.DefenderVictory)
                {
                    flag = true;
                }
            }
            else if (__instance.IsSallyOut || __instance.IsBlockadeSallyOut)
            {
                if (__instance.MapEventSettlement.Town != null && __instance.MapEventSettlement.Town.GarrisonParty != null && __instance.MapEventSettlement.Town.GarrisonParty.IsActive)
                {
                    __instance.MapEventSettlement.Town.GarrisonParty.SetMoveModeHold();
                }
                BattleState battleState = __instance.BattleState;
                if (battleState != BattleState.DefenderVictory)
                {
                    if (battleState == BattleState.AttackerVictory)
                    {
                        SiegeEvent siegeEvent2 = __instance.MapEventSettlement.SiegeEvent;
                        if (siegeEvent2 != null)
                        {
                            siegeEvent2.BesiegerCamp.RemoveAllSiegeParties();
                        }
                        CampaignEventDispatcher.Instance.SiegeCompleted(__instance.MapEventSettlement, __instance.DefenderSide.LeaderParty.MobileParty, false, __instance._mapEventType);
                    }
                }
                else
                {
                    CampaignEventDispatcher.Instance.SiegeCompleted(__instance.MapEventSettlement, __instance.DefenderSide.LeaderParty.MobileParty, true, __instance._mapEventType);
                    isWin = true;
                }
                if (__instance.BattleState == BattleState.AttackerVictory || __instance.BattleState == BattleState.DefenderVictory)
                {
                    flag = true;
                }
            }
            else if (__instance.IsBlockadeSallyOut || __instance.IsBlockade)
            {
                BattleState battleState = __instance.BattleState;
                if (battleState == BattleState.AttackerVictory)
                {
                    SiegeEvent siegeEvent3 = __instance.MapEventSettlement.SiegeEvent;
                    if (siegeEvent3 != null)
                    {
                        siegeEvent3.BesiegerCamp.RemoveAllSiegeParties();
                    }
                    CampaignEventDispatcher.Instance.SiegeCompleted(__instance.MapEventSettlement, __instance.DefenderSide.LeaderParty.MobileParty, false, __instance._mapEventType);
                }
            }
        }
        MapEventComponent component = __instance.Component;
        if (component != null)
        {
            component.BeforeFinalizeComponent();
        }
        foreach (PartyBase partyBase in __instance.InvolvedParties)
        {
            if (partyBase.IsMobile)
            {
                partyBase.MobileParty.EventPositionAdder = Vec2.Zero;
            }
            partyBase.SetVisualAsDirty();
            if (partyBase.IsMobile && partyBase.MobileParty.Army != null && partyBase.MobileParty.Army.LeaderParty == partyBase.MobileParty)
            {
                foreach (MobileParty mobileParty in partyBase.MobileParty.Army.LeaderParty.AttachedParties)
                {
                    mobileParty.Party.SetVisualAsDirty();
                }
            }
        }
        MapEventSide[] sides = __instance._sides;
        for (int i = 0; i < sides.Length; i++)
        {
            sides[i].HandleMapEventEnd();
        }
        IMapEventVisual mapEventVisual = __instance.MapEventVisual;
        if (mapEventVisual != null)
        {
            mapEventVisual.OnMapEventEnd();
        }
        if (__instance._mapEventType != MapEvent.BattleTypes.Siege && __instance._mapEventType != MapEvent.BattleTypes.SiegeOutside && __instance._mapEventType != MapEvent.BattleTypes.SallyOut)
        {
            foreach (PartyBase partyBase2 in __instance.InvolvedParties)
            {
                if (partyBase2.IsMobile && partyBase2 != PartyBase.MainParty && partyBase2.MobileParty.BesiegedSettlement != null && (partyBase2.MobileParty.Army == null || partyBase2.MobileParty.Army.LeaderParty == partyBase2.MobileParty))
                {
                    if (partyBase2.IsActive)
                    {
                        EncounterManager.StartSettlementEncounter(partyBase2.MobileParty, partyBase2.MobileParty.BesiegedSettlement);
                    }
                    else
                    {
                        partyBase2.MobileParty.BesiegerCamp = null;
                    }
                }
            }
        }
        MapEventComponent component2 = __instance.Component;
        if (component2 != null)
        {
            component2.FinalizeComponent();
        }
        if (flag)
        {
            CampaignEventDispatcher.Instance.AfterSiegeCompleted(__instance.MapEventSettlement, __instance.AttackerSide.LeaderParty.MobileParty, isWin, __instance._mapEventType);
        }
        sides = __instance._sides;
        for (int i = 0; i < sides.Length; i++)
        {
            sides[i].Clear();
        }

        return false;
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