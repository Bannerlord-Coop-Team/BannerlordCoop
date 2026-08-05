using Common;
using Common.Logging;
using GameInterface.Policies;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.SiegeEvents.Patches;

/// <summary>
/// Turns "assault the siege camp" at a friendly besieged fortification into an encounter against the
/// besiegers, instead of one that folds the player into their assault on the settlement.
/// </summary>
/// <remarks>
/// Observed live, three times: taking that option at a friendly besieged castle produced a battle whose
/// ATTACKER side held the player, the player's parties AND all thirteen besieger parties, while the
/// DEFENDER side was the castle itself and two of the player's own faction's parties. The player was
/// assaulting their own castle alongside the enemy.
///
/// Vanilla's consequence reaches that state through a branch that assumes the encountered party is the one
/// being fought. Here the encountered party is the besieger camp's leader while the settlement under siege
/// is friendly, and the resulting battle is built around the settlement's siege rather than against the
/// camp. This restarts the encounter explicitly with the pairing the option's name promises — the player
/// attacking, the besieger camp defending — and lets everything downstream follow from that.
///
/// Multi-client safety comes from the path this deliberately keeps: RestartPlayerEncounter only builds the
/// LOCAL PlayerEncounter. The battle itself is still created by
/// <c>PlayerEncounterPatches.StartBattle</c>, which forwards the encounter's attacker/defender pair to
/// <c>MapEventCreationCoordinator.RequestBlocking</c>; the server creates the single authoritative MapEvent
/// and replicates it. Nothing here creates a MapEvent locally, so the two clients cannot disagree about who
/// is on which side.
///
/// Every skip is logged with its reason. A guard that declines silently is indistinguishable from one that
/// never ran, and an earlier revision of this patch cost a full test round exactly that way.
/// </remarks>
[HarmonyPatch(typeof(EncounterGameMenuBehavior), "game_menu_join_encounter_help_defenders_on_consequence")]
internal class SiegeReliefJoinSidePatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<SiegeReliefJoinSidePatch>();

    [HarmonyPrefix]
    private static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsServer) return true;

        var settlement = ResolveBesiegedSettlement(out var source);
        var skip = WhyNotRelieve(settlement, out var besiegerLeader, out var mainParty);
        if (skip != null)
        {
            Logger.Information(
                "[ReliefDiag] leaving this to vanilla (settlement={Settlement} via {Source}): {Reason}",
                settlement?.StringId ?? "<none>", source, skip);
            return true;
        }

        Logger.Information(
            "[ReliefDiag] relieving {Settlement} (via {Source}): restarting encounter as attacker={Attacker} vs defender={Defender}",
            settlement.StringId, source, mainParty.Id, besiegerLeader.Id);

        // Parameter order is (defenderParty, attackerParty) - the besiegers defend their camp, we attack it.
        PlayerEncounter.RestartPlayerEncounter(besiegerLeader, mainParty, false, false);
        GameMenu.ActivateGameMenu("encounter");

        Logger.Information(
            "[ReliefDiag] encounter now attacker={Attacker} defender={Defender}",
            PlayerEncounter.Current?._attackerParty?.Id ?? "<none>",
            PlayerEncounter.Current?._defenderParty?.Id ?? "<none>");

        return false;
    }

    /// <summary>
    /// EncounterSettlement is populated here, but was not in an earlier revision's assumptions, so the
    /// other sources stay as fallbacks and the log records which one answered.
    /// </summary>
    private static Settlement ResolveBesiegedSettlement(out string source)
    {
        var encounterSettlement = PlayerEncounter.EncounterSettlement;
        if (encounterSettlement?.SiegeEvent != null) { source = "EncounterSettlement"; return encounterSettlement; }

        var current = Settlement.CurrentSettlement;
        if (current?.SiegeEvent != null) { source = "CurrentSettlement"; return current; }

        var battleSettlement = PlayerEncounter.EncounteredBattle?.MapEventSettlement;
        if (battleSettlement?.SiegeEvent != null) { source = "EncounteredBattle.MapEventSettlement"; return battleSettlement; }

        var besieged = PlayerEncounter.EncounteredParty?.MobileParty?.BesiegerCamp?.SiegeEvent?.BesiegedSettlement;
        if (besieged != null) { source = "EncounteredParty.BesiegerCamp"; return besieged; }

        source = "<unresolved>";
        return encounterSettlement ?? current;
    }

    private static string WhyNotRelieve(Settlement settlement, out PartyBase besiegerLeader, out PartyBase mainParty)
    {
        besiegerLeader = null;
        mainParty = MobileParty.MainParty?.Party;

        if (settlement == null) return "no besieged settlement in this encounter";
        if (settlement.SiegeEvent == null) return "settlement is not under siege";

        var settlementFaction = settlement.MapFaction;
        var playerFaction = MobileParty.MainParty?.MapFaction;
        if (settlementFaction == null) return "settlement has no faction";
        if (playerFaction == null) return "player has no faction";

        // At war with the settlement means WE are the besieging side; vanilla's handling is correct there.
        if (settlementFaction.IsAtWarWith(playerFaction)) return "at war with the settlement - we are the besieger";

        besiegerLeader = settlement.SiegeEvent.BesiegerCamp?.LeaderParty?.Party;
        if (besiegerLeader == null) return "siege has no besieger camp leader to attack";
        if (besiegerLeader == mainParty) return "we lead the besieger camp ourselves";

        if (mainParty == null) return "no main party";
        if (mainParty.MapEvent != null) return "already in a battle";

        // If the besiegers are already fighting, joining that battle is a different flow than starting one.
        if (besiegerLeader.MapEvent != null) return "besieger leader is already in a battle";

        return null;
    }
}
