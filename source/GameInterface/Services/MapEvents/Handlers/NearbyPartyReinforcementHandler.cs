using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Configuration;
using GameInterface.Services.MapEvents.Patches;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.PlayerCaptivityService.Messages;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Handlers;

/// <summary>
/// Server-side replacement for vanilla's nearby-party reinforcement, so AI parties standing next to a
/// player's battle actually join it.
/// </summary>
/// <remarks>
/// Vanilla does this from <c>PlayerEncounter.CheckNearbyPartiesToJoinPlayerMapEvent</c>, driven off
/// PlayerEncounter.Update. Co-op suppresses that method outright, because on a client it would mutate the
/// shared MapEventSide locally and desync - so nothing ever pulled nearby parties in and a friendly army
/// could sit beside your battle doing nothing.
///
/// The selection itself is vanilla's and needs no porting: PlayerEncounter's method is a one-line delegate to
/// <c>EncounterModel.FindNonAttachedNpcPartiesWhoWillJoinPlayerEncounter(list, list)</c>, which takes only the
/// two side lists - no MainParty, no encounter state - so the headless host can call it directly.
///
/// Replication is already in place: <see cref="MapEventPatches"/>' AddInvolvedPartyInternal postfix
/// broadcasts an AI join while the battle is inside its
/// <see cref="ModConfigProvider.ModOptions.PlayerBattleAiJoinWindowHours"/> window. That window existed with
/// nothing to populate it; this is what populates it.
/// </remarks>
internal class NearbyPartyReinforcementHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<NearbyPartyReinforcementHandler>();

    private readonly IMessageBroker messageBroker;

    public NearbyPartyReinforcementHandler(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;
        messageBroker.Subscribe<PlayerJoinedBattle>(Handle_PlayerJoinedBattle);
        messageBroker.Subscribe<CampaignTick>(Handle_CampaignTick);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerJoinedBattle>(Handle_PlayerJoinedBattle);
        messageBroker.Unsubscribe<CampaignTick>(Handle_CampaignTick);
    }

    /// <summary>
    /// The moment a player's battle opens its AI-join window is the one moment reinforcement is guaranteed to
    /// get a look in. CampaignTick alone is not enough: map time stops while a player sits in an encounter, so
    /// a tick-driven scan can go the entire battle without running - which is why nearby lords stood and
    /// watched. This fires from MapEvent.Initialize's postfix, where the window is opened.
    /// </summary>
    private void Handle_PlayerJoinedBattle(MessagePayload<PlayerJoinedBattle> payload)
    {
        if (!ModInformation.IsServer) return;

        // Published with the MapEvent as its source; E2E publishes the same event with a test object, so type-check.
        if (payload.Who is not MapEvent mapEvent) return;

        var skip = WhyNotReinforce(mapEvent);
        if (skip != null)
        {
            Logger.Debug("[Reinforce] battle {MapEventId} opened its join window but will not reinforce: {Reason}",
                mapEvent.StringId ?? "<no id>", skip);
            return;
        }

        // Never let a reinforcement failure take a battle down with it - the battle is playable without it.
        try
        {
            Reinforce(mapEvent);
        }
        catch (System.Exception e)
        {
            Logger.Error(e, "Reinforcing player battle {MapEventId} at start failed", mapEvent.StringId ?? "<no id>");
        }
    }

    private void Handle_CampaignTick(MessagePayload<CampaignTick> payload)
    {
        if (!ModInformation.IsServer) return;

        var events = Campaign.Current?.MapEventManager?.MapEvents;
        if (events == null) return;

        // ToArray: adding a party mutates the event graph while we walk it.
        foreach (var mapEvent in events.ToArray())
        {
            var skip = WhyNotReinforce(mapEvent);
            if (skip != null)
            {
                // Only trace player battles - an ordinary AI skirmish skipping this is not interesting.
                if (mapEvent != null && mapEvent.InvolvedParties.Any(p => p.IsMobile && p.MobileParty?.IsPlayerParty() == true))
                    Logger.Debug("[Reinforce] skipping player battle {MapEventId}: {Reason}",
                        mapEvent.StringId ?? "<no id>", skip);
                continue;
            }

            Reinforce(mapEvent);
        }
    }

    /// <summary>
    /// Mirrors vanilla's own guards, plus the co-op join window. Returns null when the event SHOULD be
    /// reinforced, otherwise the reason it was skipped - so "no reinforcement" is diagnosable instead of
    /// silent. A bare "nothing happened" is indistinguishable from "never ran", which cost real time.
    /// </summary>
    private static string WhyNotReinforce(MapEvent mapEvent)
    {
        if (mapEvent == null) return "null";
        if (mapEvent.IsFinalized) return "finalized";

        // Vanilla refuses these outright - a raid, a wall assault, and the forced supply/volunteer shakedowns
        // are not battles nearby parties may wander into.
        if (mapEvent.IsRaid) return "raid";
        if (mapEvent.IsSiegeAssault) return "siege assault";
        if (mapEvent.IsForcingSupplies) return "forcing supplies";
        if (mapEvent.IsForcingVolunteers) return "forcing volunteers";

        if (mapEvent.MapEventSettlement?.IsHideout == true) return "hideout";

        // Only player battles reinforce, and only while the window the broadcast path checks is still open -
        // otherwise the join would apply on the server and never reach the clients.
        if (!InteractionPatches.IsWithinAiJoinWindow(mapEvent))
            return "outside the AI join window (none opened, or it expired)";

        if (!mapEvent.InvolvedParties.Any(p => p.IsMobile && p.MobileParty?.IsPlayerParty() == true))
            return "no player party involved";

        return null;
    }

    private static void Reinforce(MapEvent mapEvent)
    {
        var attackers = CollectMobileParties(mapEvent, BattleSideEnum.Attacker);
        var defenders = CollectMobileParties(mapEvent, BattleSideEnum.Defender);

        var attackerCount = attackers.Count;
        var defenderCount = defenders.Count;

        var model = Campaign.Current?.Models?.EncounterModel;
        if (model == null) return;

        // Vanilla appends the parties that would join to each list in place.
        model.FindNonAttachedNpcPartiesWhoWillJoinPlayerEncounter(attackers, defenders);

        Logger.Debug("[Reinforce] {MapEventId}: model offered {Att} attacker / {Def} defender joiners",
            mapEvent.StringId ?? "<no id>",
            attackers.Count - attackerCount,
            defenders.Count - defenderCount);

        AddJoiners(mapEvent, BattleSideEnum.Attacker, attackers, attackerCount);
        AddJoiners(mapEvent, BattleSideEnum.Defender, defenders, defenderCount);
    }

    private static List<MobileParty> CollectMobileParties(MapEvent mapEvent, BattleSideEnum side)
    {
        var parties = new List<MobileParty>();

        var onSide = mapEvent.PartiesOnSide(side);
        if (onSide == null) return parties;

        foreach (var mapEventParty in onSide)
        {
            if (mapEventParty?.Party?.IsMobile != true) continue;
            parties.Add(mapEventParty.Party.MobileParty);
        }

        return parties;
    }

    /// <summary>Adds only the entries the model appended, leaving the parties already in the battle alone.</summary>
    private static void AddJoiners(MapEvent mapEvent, BattleSideEnum side, List<MobileParty> parties, int alreadyPresent)
    {
        if (parties.Count <= alreadyPresent) return;

        var mapEventSide = mapEvent.GetMapEventSide(side);
        if (mapEventSide == null) return;

        for (var i = alreadyPresent; i < parties.Count; i++)
        {
            var party = parties[i];
            if (party == null) continue;

            // A player party never joins by proximity - it chooses through its own encounter menu.
            if (party.IsPlayerParty()) continue;

            Logger.Debug("Nearby party {PartyId} joins the player battle on the {Side} side",
                party.StringId, side);
            mapEventSide.AddNearbyPartyToPlayerMapEvent(party);
        }
    }
}
