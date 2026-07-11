using Common;
using Common.Messaging;
using GameInterface.Services.SiegeEvents.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.SiegeEvents.Patches;

/// <summary>
/// Announces siege preparation starting and a siege dissolving without a battle on the server, so a
/// player inside the settlement gets the vanilla menus. Vanilla switches the inside player from its
/// own campaign tick, which never runs the interrupt on a co-op client parked at the static town
/// menu, and the replicated SiegeEvent is built without its constructor so no client-side campaign
/// event ever fires.
/// </summary>
[HarmonyPatch]
internal class SiegePreparationPromptPatches
{
    // StartSiegeEvent is the single funnel for player- and AI-started sieges; a postfix runs after
    // the whole SiegeEvent constructor, so every siege graph message precedes the prompt on the wire.
    [HarmonyPatch(typeof(SiegeEventManager), nameof(SiegeEventManager.StartSiegeEvent))]
    [HarmonyPostfix]
    private static void StartSiegeEventPostfix(Settlement settlement, MobileParty besiegerParty)
    {
        if (ModInformation.IsClient) return;
        if (settlement == null || besiegerParty == null) return;

        MessageBroker.Instance.Publish(null, new SiegePreparationStarted(besiegerParty, settlement));
    }

    // Only a siege that dissolves without an assault battle needs the prompt; a siege ending
    // through a battle hands the inside player over via the mission and aftermath flows.
    [HarmonyPatch(typeof(SiegeEvent), nameof(SiegeEvent.FinalizeSiegeEvent))]
    [HarmonyPostfix]
    private static void FinalizeSiegeEventPostfix(SiegeEvent __instance)
    {
        if (ModInformation.IsClient) return;

        var settlement = __instance.BesiegedSettlement;
        if (settlement == null || settlement.Party?.MapEvent != null) return;

        MessageBroker.Instance.Publish(null, new SiegeEndedWithoutBattle(settlement, __instance._isBesiegerDefeated));
    }
}
