using Common.Logging;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Handlers;

internal static class MapEventHostileActionConsequences
{
    private static readonly ILogger Logger = LogManager.GetLogger(typeof(MapEventHostileActionConsequences));

    // The -10 player-hostility relation penalty vanilla applies against the target faction leader
    // when an attack first turns a faction hostile (the war block of BeHostileAction).
    private const int PlayerHostilityRelationPenalty = -10;

    internal static void Apply(MapEvent mapEvent, PartyBase attackerParty, string source)
    {
        try
        {
            if (Campaign.Current == null)
                return;

            if (attackerParty == null)
            {
                Logger.Warning("Could not resolve attacker party for {Source} hostile-action consequences", source);
                return;
            }

            // OpponentSide is only meaningful while the attacker is a side in this event.
            if (attackerParty.MapEvent != mapEvent)
                return;

            var defenderParty = mapEvent.GetLeaderParty(attackerParty.OpponentSide);
            if (defenderParty == null)
                return;

            var attackerFaction = GetMapFaction(attackerParty.MapFaction);
            var defenderFaction = GetMapFaction(defenderParty.MapFaction);
            if (attackerFaction == null || defenderFaction == null || attackerFaction == defenderFaction)
                return;

            if (Campaign.Current.Models.EncounterModel.IsEncounterExemptFromHostileActions(attackerParty, defenderParty))
                return;

            // Already hostile: nothing to declare, and this is what makes the consequence idempotent
            // across duplicate attack/join requests a client can send during the server round-trip.
            if (FactionManager.IsAtWarAgainstFaction(attackerFaction, defenderFaction))
                return;

            if (attackerParty.LeaderHero != null && defenderFaction.Leader != null)
            {
                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(attackerParty.LeaderHero, defenderFaction.Leader, PlayerHostilityRelationPenalty);
            }

            Logger.Debug("Applying {Source} hostile-action war between {AttackerFaction} and {DefenderFaction}", source, attackerFaction.Name, defenderFaction.Name);
            DeclareWarAction.ApplyByPlayerHostility(attackerFaction, defenderFaction);
            ApplyWarStance(attackerFaction, defenderFaction);
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to apply {Source} hostile-action consequences", source);
        }
    }

    private static void ApplyWarStance(IFaction attackerFaction, IFaction defenderFaction)
    {
        if (FactionManager.IsAtWarAgainstFaction(attackerFaction, defenderFaction))
            return;

        var stanceLink = FactionManager.Instance.GetStanceLinkInternal(attackerFaction, defenderFaction);
        if (stanceLink.StanceType == StanceType.War)
            return;

        stanceLink.StanceType = StanceType.War;
        attackerFaction.UpdateFactionsAtWarWith();
        defenderFaction.UpdateFactionsAtWarWith();
    }

    private static IFaction GetMapFaction(IFaction faction)
    {
        return faction?.MapFaction ?? faction;
    }
}