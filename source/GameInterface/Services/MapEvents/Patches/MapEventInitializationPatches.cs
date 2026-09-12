using Common;
using GameInterface.Policies;
using GameInterface.Services.MapEvents.Initialization;
using HarmonyLib;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch]
internal static class MapEventInitializationPatches
{
    [HarmonyPatch(typeof(MapEvent), MethodType.Constructor)]
    [HarmonyFinalizer]
    [HarmonyPriority(Priority.First)]
    private static Exception Finalizer_MapEventConstructor(MapEvent __instance, Exception __exception)
    {
        if (__exception != null && TryResolve(out var barrier)) barrier.AbortServer(__instance);
        return __exception;
    }

    [HarmonyPatch(typeof(PartyBase), nameof(PartyBase.MapEventSide), MethodType.Setter)]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void Prefix_PartyBaseMapEventSide(PartyBase __instance, MapEventSide value, out bool __state)
    {
        __state = false;
        if (ReferenceEquals(__instance?.MapEventSide, value) || value?.MapEvent == null ||
            !TryResolve(out var barrier)) return;
        barrier.SetServerPartyPending(value.MapEvent, __instance, true);
        __state = true;
    }

    [HarmonyPatch(typeof(PartyBase), nameof(PartyBase.MapEventSide), MethodType.Setter)]
    [HarmonyFinalizer]
    [HarmonyPriority(Priority.First)]
    private static Exception Finalizer_PartyBaseMapEventSide(
        PartyBase __instance,
        MapEventSide value,
        bool __state,
        Exception __exception)
    {
        if (__state && value?.Parties?.Any(party => ReferenceEquals(party?.Party, __instance)) != true &&
            value?.MapEvent != null && TryResolve(out var barrier))
            barrier.SetServerPartyPending(value.MapEvent, __instance, false);
        return __exception;
    }

    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.Initialize))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void Prefix_MapEventInitialize(MapEvent __instance, PartyBase attackerParty, PartyBase defenderParty)
    {
        if (!TryResolve(out var barrier)) return;
        barrier.SetServerPartyPending(__instance, attackerParty, true);
        barrier.SetServerPartyPending(__instance, defenderParty, true);
    }

    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.Initialize))]
    [HarmonyFinalizer]
    [HarmonyPriority(Priority.First)]
    private static Exception Finalizer_MapEventInitialize(MapEvent __instance, Exception __exception)
    {
        if (__exception != null && TryResolve(out var barrier)) barrier.AbortServer(__instance);
        return __exception;
    }

    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.AddInvolvedPartyInternal))]
    [HarmonyPostfix]
    private static void Postfix_MapEventAddInvolvedParty(MapEvent __instance, MapEventParty mapEventParty)
    {
        if (TryResolve(out var barrier)) barrier.TrackParty(__instance, mapEventParty);
    }

    [HarmonyPatch(typeof(MapEventManager), nameof(MapEventManager.OnMapEventCreated))]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix_MapEventCreated(MapEvent mapEvent)
    {
        if (TryResolve(out var barrier)) barrier.CommitServer(mapEvent);
    }

    private static bool TryResolve(out IMapEventInitializationBarrier barrier)
    {
        barrier = null;
        return ModInformation.IsServer &&
            !CallOriginalPolicy.IsOriginalAllowed() &&
            ContainerProvider.TryResolve(out barrier);
    }
}
