using Common;
using GameInterface.Services.SiegeEvents.Interfaces;
using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.SiegeEvents.Patches;

/// <summary>
/// Vanilla's <c>KingdomManager.SiegeCompleted</c> enters the capturers while the battle still holds them, which
/// <c>EnterSettlementActionPatches</c> refuses, so the server hands the besiegers to
/// <see cref="ISiegeEventInterface.EnterAiCapturers"/> once the battle lets go of them.
/// </summary>
[HarmonyPatch(typeof(MapEvent), nameof(MapEvent.FinalizeEventAux))]
internal class SiegeCaptureEntryPatches
{
    [HarmonyPrefix]
    private static void Prefix(MapEvent __instance, bool __runOriginal, out SiegeCapture __state)
    {
        __state = null;
        if (ModInformation.IsClient || !__runOriginal) return;

        var settlement = __instance.MapEventSettlement;
        var besiegerCamp = settlement?.SiegeEvent?.BesiegerCamp;
        if (besiegerCamp == null) return;

        // The parties SiegeCompleted enters, before it removes them from the camp
        var besiegers = besiegerCamp.GetInvolvedPartiesForEventType().Select(party => party.MobileParty).ToArray();
        __state = new SiegeCapture(settlement, settlement.MapFaction, besiegers);
    }

    // Runs after MapEventPatches sends the destroy, otherwise clients still have the parties in the battle and refuse the entry
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Low)]
    private static void Postfix(bool __runOriginal, SiegeCapture __state)
    {
        if (!__runOriginal || __state == null) return;

        if (ContainerProvider.TryResolve<ISiegeEventInterface>(out var siegeEventInterface))
            siegeEventInterface.EnterAiCapturers(__state.Settlement, __state.FactionBeforeBattle, __state.Besiegers);
    }

    private class SiegeCapture
    {
        public SiegeCapture(Settlement settlement, IFaction factionBeforeBattle, MobileParty[] besiegers)
        {
            Settlement = settlement;
            FactionBeforeBattle = factionBeforeBattle;
            Besiegers = besiegers;
        }

        public Settlement Settlement { get; }
        public IFaction FactionBeforeBattle { get; }
        public MobileParty[] Besiegers { get; }
    }
}
