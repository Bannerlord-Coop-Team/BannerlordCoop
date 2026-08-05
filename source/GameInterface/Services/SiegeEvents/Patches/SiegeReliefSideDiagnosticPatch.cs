using Common.Logging;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.SiegeEvents.Patches;

/// <summary>
/// Records which side a player ends up on after taking one of the siege-relief menu options, and what the
/// encounter looked like going in.
/// </summary>
/// <remarks>
/// Read-only: it changes no behaviour, it only makes an ambiguous report answerable. "Assault the siege
/// camp" against a settlement runs vanilla's help_defenders consequence, which joins as Defender, while
/// engaging the besieging army directly is an ordinary hostile encounter that joins as Attacker. Both look
/// identical in a battle log afterwards — the resulting MapEvent just says "Attacker" either way — so a
/// report of "we joined the wrong side" cannot be diagnosed without knowing which action was taken.
/// </remarks>
[HarmonyPatch]
internal class SiegeReliefSideDiagnosticPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<SiegeReliefSideDiagnosticPatch>();

    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(EncounterGameMenuBehavior), "game_menu_join_encounter_help_defenders_on_consequence");
        yield return AccessTools.Method(typeof(EncounterGameMenuBehavior), "game_menu_join_encounter_help_attackers_on_consequence");
    }

    [HarmonyPostfix]
    private static void Postfix(MethodBase __originalMethod)
    {
        try
        {
            var mainParty = MobileParty.MainParty?.Party;
            var settlement = PlayerEncounter.EncounterSettlement;

            var mapEvent = mainParty?.MapEvent;

            Logger.Information(
                "[ReliefDiag] {Consequence}: settlement={Settlement} besieged={Besieged} encounteredParty={Encountered} " +
                "-> playerSide={Side} mapEvent={MapEvent}",
                __originalMethod?.Name,
                settlement?.StringId ?? "<none>",
                settlement?.SiegeEvent != null,
                PlayerEncounter.EncounteredParty?.Id ?? "<none>",
                mainParty?.MapEventSide?.MissionSide,
                mapEvent?.StringId ?? "<none>");

            // The side LABEL alone cannot tell a relief apart from accidentally reinforcing the besiegers:
            // "Assault the siege camp" legitimately makes the player the attacker of the besieger camp. What
            // separates the two is who shares the player's side, so name both sides' members.
            DescribeSide(mapEvent, BattleSideEnum.Attacker, mainParty);
            DescribeSide(mapEvent, BattleSideEnum.Defender, mainParty);
        }
        catch
        {
            // A diagnostic must never be the reason a menu option fails.
        }
    }

    private static void DescribeSide(MapEvent mapEvent, BattleSideEnum side, PartyBase mainParty)
    {
        var mapEventSide = mapEvent?.GetMapEventSide(side);
        if (mapEventSide == null) return;

        var members = new List<string>();
        foreach (var mapEventParty in mapEventSide.Parties)
        {
            var party = mapEventParty?.Party;
            if (party == null) continue;

            members.Add($"{party.Id}[{party.MapFaction?.StringId ?? "?"}]{(party == mainParty ? "<-YOU" : "")}");
        }

        Logger.Information(
            "[ReliefDiag]   {Side} side: leader={Leader} faction={Faction} members={Members}",
            side,
            mapEventSide.LeaderParty?.Id ?? "<none>",
            mapEventSide.LeaderParty?.MapFaction?.StringId ?? "<none>",
            string.Join(", ", members));
    }
}
