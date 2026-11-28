using Common;
using Common.Logging;
using GameInterface.Services.SiegeEvents.Interfaces;
using HarmonyLib;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.SiegeEvents.Patches;

/// <summary>
/// Vanilla's <c>KingdomManager.SiegeCompleted</c> enters the capturers while the battle still holds them, which
/// <c>EnterSettlementActionPatches</c> refuses, so the server enters a player capture leader and the AI besiegers
/// through <see cref="ISiegeEventInterface"/> once the battle lets go of them.
/// </summary>
[HarmonyPatch(typeof(MapEvent), nameof(MapEvent.FinalizeEventAux))]
internal class SiegeCaptureEntryPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<SiegeCaptureEntryPatches>();

    [HarmonyPrefix]
    private static void Prefix(MapEvent __instance, bool __runOriginal, out SiegeCapture __state)
    {
        __state = null;
        if (ModInformation.IsClient || !__runOriginal) return;

        var settlement = __instance.MapEventSettlement;
        if (settlement == null) return;

        // Read before finalizing clears the battle sides
        var playerCaptureLeader = SiegeAftermathPatches.GetPlayerCaptureLeader(__instance);
        // The parties SiegeCompleted enters, before it removes them from the camp
        var besiegers = settlement.SiegeEvent?.BesiegerCamp?.GetInvolvedPartiesForEventType().Select(party => party.MobileParty).ToArray()
            ?? Array.Empty<MobileParty>();
        if (playerCaptureLeader == null && besiegers.Length == 0) return;

        __state = new SiegeCapture(settlement, settlement.MapFaction, besiegers, playerCaptureLeader);
    }

    // Runs after MapEventPatches sends the destroy, otherwise clients still have the parties in the battle and refuse the entry
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Low)]
    private static void Postfix(bool __runOriginal, SiegeCapture __state)
    {
        if (!__runOriginal || __state == null) return;
        if (!ContainerProvider.TryResolve<ISiegeEventInterface>(out var siegeEventInterface)) return;

        // Caught here, otherwise Finalizer_FinalizeEventAux reports it as a failed finalize and releases a finished battle again
        try
        {
            if (__state.PlayerCaptureLeader != null)
                siegeEventInterface.EnterPendingCaptureLeader(__state.PlayerCaptureLeader, __state.Settlement);

            siegeEventInterface.EnterAiCapturers(__state.Settlement, __state.FactionBeforeBattle, __state.Besiegers);
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to enter the capturers of {Settlement}", __state.Settlement.StringId);
        }
    }

    private class SiegeCapture
    {
        public SiegeCapture(Settlement settlement, IFaction factionBeforeBattle, MobileParty[] besiegers, MobileParty playerCaptureLeader)
        {
            Settlement = settlement;
            FactionBeforeBattle = factionBeforeBattle;
            Besiegers = besiegers;
            PlayerCaptureLeader = playerCaptureLeader;
        }

        public Settlement Settlement { get; }
        public IFaction FactionBeforeBattle { get; }
        public MobileParty[] Besiegers { get; }
        public MobileParty PlayerCaptureLeader { get; }
    }
}
