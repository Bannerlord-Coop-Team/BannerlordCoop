#if DEBUG
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Locations;
using GameInterface.Services.Locations.Handlers;
using GameInterface.Services.Locations.Messages;
using HarmonyLib;
using Serilog;
using System.Threading;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Overlay;

namespace GameInterface.Services.Locations.Patches;

internal class Issue1837WitnessPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<Issue1837WitnessPatches>();

    [HarmonyPatch(typeof(MapEvent), "FinalizeEventAux")]
    private class Finalize
    {
        private static void Prefix(MapEvent __instance) =>
            Logger.Information("[Issue1837Witness] finalize settlement={Settlement} siege={Siege} winner={Winner} leader={Leader} thread={Thread}",
                __instance.MapEventSettlement?.StringId, __instance.IsSiegeAssault, __instance.WinningSide,
                __instance.AttackerSide?.LeaderParty?.MobileParty?.StringId, Thread.CurrentThread.ManagedThreadId);
    }

    [HarmonyPatch(typeof(SettlementPopulationTracker), "OnPartyEnteredSettlement")]
    private class Entry
    {
        private static void Prefix(Settlement settlement, MobileParty party) =>
            Logger.Information("[Issue1837Witness] entry {Settlement} {Party} thread={Thread}",
                settlement?.StringId, party?.StringId, Thread.CurrentThread.ManagedThreadId);
    }

    [HarmonyPatch(typeof(SettlementPopulationTracker), "BroadcastRosterSnapshot")]
    private class Send
    {
        private static void Prefix(string settlementId) =>
            Logger.Information("[Issue1837Witness] snapshot-send {Settlement} thread={Thread}",
                settlementId, Thread.CurrentThread.ManagedThreadId);
    }

    [HarmonyPatch(typeof(LocationHandler), "Handle_NetworkLocationRosterSnapshot")]
    private class Receive
    {
        private static void Prefix(MessagePayload<NetworkLocationRosterSnapshot> payload) =>
            Logger.Information("[Issue1837Witness] snapshot-receive {Settlement} entries={Count} sent={Sent} thread={Thread}",
                payload.What.SettlementId, payload.What.Entries?.Length, payload.When, Thread.CurrentThread.ManagedThreadId);
    }

    [HarmonyPatch(typeof(LocationHandler), "ReconcileSettlementRosters")]
    private class Apply
    {
        private static void Postfix(LocationCharacterData[] entries) =>
            Logger.Information("[Issue1837Witness] snapshot-apply entries={Count} thread={Thread}",
                entries?.Length, Thread.CurrentThread.ManagedThreadId);
    }

    [HarmonyPatch(typeof(SettlementMenuOverlayVM), "Refresh")]
    private class Overlay
    {
        private static void Prefix(SettlementMenuOverlayVM __instance) => Record("overlay-before", __instance);
        private static void Postfix(SettlementMenuOverlayVM __instance) => Record("overlay-after", __instance);
        private static void Record(string phase, SettlementMenuOverlayVM overlay) =>
            Logger.Information("[Issue1837Witness] {Phase} {Settlement} characters={Count} thread={Thread}",
                phase, overlay?._settlement?.StringId, overlay?.CharacterList?.Count, Thread.CurrentThread.ManagedThreadId);
    }
}
#endif
