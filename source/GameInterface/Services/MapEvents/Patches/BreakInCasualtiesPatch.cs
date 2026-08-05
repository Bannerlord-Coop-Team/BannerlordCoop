using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MapEvents.Messages.Retreat;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Routes the break-in's troop losses through the server instead of letting the client charge itself.
/// </summary>
/// <remarks>
/// Vanilla's ApplyBreakIn picks victims with a local MBRandom draw and removes them straight out of
/// MobileParty.MainParty's roster (spreading across the army when the party leads one). Co-op has no route
/// for that at all - the client mutated its own roster and replicated nothing, so every other machine kept
/// the pre-break-in troops. The client now announces the intent and the server applies the authoritative
/// loss, which arrives back as ordinary roster deltas.
///
/// The out parameters are zeroed on the client, so the debrief menu reports no casualties locally even
/// though the server does remove them. That is a display gap, not a state gap - the rosters converge.
/// </remarks>
[HarmonyPatch(typeof(BreakInOutBesiegedSettlementAction))]
internal class BreakInCasualtiesPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<BreakInCasualtiesPatch>();

    [HarmonyPatch(nameof(BreakInOutBesiegedSettlementAction.ApplyBreakIn))]
    [HarmonyPrefix]
    private static bool ApplyBreakInPrefix(ref TroopRoster casualties, ref int armyCasualtiesCount)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsServer) return true;

        casualties = TroopRoster.CreateDummyTroopRoster();
        armyCasualtiesCount = 0;

        var mainParty = MobileParty.MainParty;
        if (mainParty == null) return false;

        var settlement = ResolveBrokenIntoSettlement(mainParty);
        if (settlement == null)
        {
            // Nothing to ask the server for, and vanilla has already been suppressed above. Say so:
            // silently returning here is how the break-in ends up costing nobody anything.
            Logger.Warning(
                "Break-in casualties skipped for {PartyId}: no settlement could be resolved for the encounter",
                mainParty.StringId);
            return false;
        }

        MessageBroker.Instance.Publish(mainParty, new BreakInCasualtiesAttempted(mainParty, settlement));
        return false;
    }

    /// <summary>
    /// The settlement being broken into.
    /// </summary>
    /// <remarks>
    /// The encounter comes first because it is the only one that holds during a normal break-in: the party
    /// is still OUTSIDE the walls and is not the besieger, so CurrentSettlement and BesiegedSettlement are
    /// both null at this point. Reading only those meant the method suppressed vanilla and then returned
    /// without ever requesting the loss, so breaking in was free.
    ///
    /// The other two stay as fallbacks for the cases where they do hold - breaking OUT from inside, and a
    /// besieger breaking through its own siege.
    /// </remarks>
    private static Settlement ResolveBrokenIntoSettlement(MobileParty mainParty)
    {
        return PlayerEncounter.EncounterSettlement
            ?? mainParty.MapEvent?.MapEventSettlement
            ?? mainParty.CurrentSettlement
            ?? mainParty.BesiegedSettlement;
    }
}
