using Common;
using Common.Messaging;
using GameInterface.Services.MobileParties.Messages.Behavior;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Settlements.Patches;

[HarmonyPatch(typeof(DefaultEncounterGameMenuModel))]
internal class EncounterGameMenuModelPatches
{
#if DEBUG
    private static readonly Serilog.ILogger Logger = Common.Logging.LogManager.GetLogger<EncounterGameMenuModelPatches>();
#endif
    [HarmonyPatch(nameof(DefaultEncounterGameMenuModel.GetGenericStateMenu))]
    [HarmonyPostfix]
    private static void Postfix(ref string __result)
    {
        if (ModInformation.IsServer || __result != "join_encounter" || PlayerEncounter.Current != null)
            return;

        var mainParty = MobileParty.MainParty;
        var settlement = mainParty.CurrentSettlement;
        if (settlement == null)
            return;

#if DEBUG
        Logger.Debug(
            "SettlementEncounterRecovery phase=trigger party={PartyStringId} settlement={SettlementStringId} settlementBattle={HasSettlementBattle} partyBattle={HasPartyBattle} encounter={HasEncounter}",
            mainParty.StringId, settlement.StringId, settlement.Party?.MapEvent != null,
            mainParty.Party?.MapEvent != null, PlayerEncounter.Current != null);
#endif
        // Settlement state can arrive before the encounter that the join menu dereferences.
        __result = null;
        MessageBroker.Instance.Publish(null, new StartSettlementEncounterAttempted(mainParty, settlement, isAutomaticRecovery: true));
    }

    [HarmonyPatch(nameof(DefaultEncounterGameMenuModel.GetEncounterMenu))]
    [HarmonyPostfix]
    private static void EncounterMenuPostfix(PartyBase attackerParty, PartyBase defenderParty, ref string __result)
    {
        if (ModInformation.IsServer || __result != "encounter")
            return;

        var mainParty = MobileParty.MainParty;
        var settlement = mainParty.CurrentSettlement;
        if (settlement == null || attackerParty != mainParty.Party || defenderParty != settlement.Party ||
            mainParty.MapEvent != null || settlement.Party.MapEvent?.IsSiegeAssault != true ||
            mainParty.MapFaction == settlement.MapFaction)
            return;

        // The combat menu starts a new battle when the player has not joined this assault yet.
        __result = "join_encounter";
    }
}
