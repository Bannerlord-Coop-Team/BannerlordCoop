using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MapEvents.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch(typeof(MapEvent))]
internal class MapEventDisablePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<MapEventCollectionPatches>();

    [HarmonyPatch(nameof(MapEvent.Initialize))]
    [HarmonyPrefix]
    static bool InitializePrefix()
    {
        // Call original if we called it
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(MapEvent), Environment.StackTrace);
            return false;
        }

        return true;
    }

    [HarmonyPatch(nameof(MapEvent.FinalizeEventAux))]
    [HarmonyPrefix]
    static bool TestPRefix(MapEvent __instance)
    {
        if (__instance.IsFinalized)

        {
            return false;
        }
        __instance.State = MapEventState.WaitingRemoval;
        CampaignEventDispatcher.Instance.OnMapEventEnded(__instance);

        bool flag = false;
        if (__instance.MapEventSettlement != null)

        {
            if ((__instance.IsSiegeAssault || __instance.IsSiegeOutside || __instance.IsSallyOut) && __instance.MapEventSettlement.SiegeEvent != null)
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
                        flag = true;
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
            }
            else if (__instance.IsSallyOut && __instance.MapEventSettlement.Town != null && __instance.MapEventSettlement.Town.GarrisonParty != null && __instance.MapEventSettlement.Town.GarrisonParty.IsActive)
            {
                __instance.MapEventSettlement.Town.GarrisonParty.Ai.SetMoveModeHold();
            }
            MapEventComponent component = __instance.Component;
            if (component != null)
            {
                component.FinalizeComponent();
            }
        }
        foreach (MapEventSide mapEventSide in __instance._sides)
        {
            mapEventSide.UpdatePartiesMoveState();
            mapEventSide.HandleMapEventEnd();
        }
        IMapEventVisual mapEventVisual = __instance.MapEventVisual;
        if (mapEventVisual != null)
        {
            mapEventVisual.OnMapEventEnd();
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
        if (flag)
        {
            __instance.MapEventSettlement.Militia += (float)Campaign.Current.Models.SettlementMilitiaModel.MilitiaToSpawnAfterSiege(__instance.MapEventSettlement.Town);
        }
        MapEventSide[] sides = __instance._sides;
        for (int i = 0; i < sides.Length; i++)
        {
            sides[i].Clear();
        }

        return false;
    }
}
