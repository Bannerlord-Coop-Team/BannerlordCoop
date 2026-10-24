using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.Armies.Patches;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MobileParties.Messages.Unstuck;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Settlements.Interfaces;
using GameInterface.Services.SiegeEvents.Interfaces;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties;

/// <summary>
/// Server steps of the unstuck flow, shared by a player's own coop.unstuck request and an
/// operator's coop.unstuck on the server.
/// </summary>
internal interface IServerPlayerUnstuck
{
    /// <summary>
    /// Force-applies every applicable exit for the player's registered party and hero, releases every
    /// party (not only the player's) still linked to a battle that already ended, ends battles left with no
    /// healthy troops on one side, then moves a free party off a map spot it cannot move from, each step
    /// guarded independently, and sends the result to the player's connection when it has one. Runs on
    /// the game thread.
    /// </summary>
    /// <returns>One line per step applied, skipped or failed.</returns>
    string[] Apply(Player player, bool includeCaptivity);
}

/// <inheritdoc cref="IServerPlayerUnstuck"/>
internal class ServerPlayerUnstuck : IServerPlayerUnstuck
{
    private static readonly ILogger Logger = LogManager.GetLogger<ServerPlayerUnstuck>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly ISettlementInterface settlementInterface;
    private readonly ISiegeEventInterface siegeEventInterface;
    private readonly IPartyNavigationRepair partyNavigationRepair;
    private readonly IMapEventLoadCleaner mapEventLoadCleaner;
    private readonly IStalledMapEventResolver stalledMapEventResolver;

    public ServerPlayerUnstuck(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        ISettlementInterface settlementInterface,
        ISiegeEventInterface siegeEventInterface,
        IPartyNavigationRepair partyNavigationRepair,
        IMapEventLoadCleaner mapEventLoadCleaner,
        IStalledMapEventResolver stalledMapEventResolver)
    {
        this.partyNavigationRepair = partyNavigationRepair;
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.settlementInterface = settlementInterface;
        this.siegeEventInterface = siegeEventInterface;
        this.mapEventLoadCleaner = mapEventLoadCleaner;
        this.stalledMapEventResolver = stalledMapEventResolver;
    }

    public string[] Apply(Player player, bool includeCaptivity)
    {
        var partyId = player.MobilePartyId;
        var actions = new List<string>();

        if (!objectManager.TryGetObjectWithLogging(partyId, out MobileParty party))
        {
            actions.Add($"Party '{partyId}' was not found on the server; nothing was applied.");
            return SendResult(player, partyId, actions);
        }

        var hero = ResolvePlayerHero(player, party);

        // Every step runs with patches live (no AllowedThread) so the applied changes replicate to
        // clients through their normal sync flows, and each step is guarded independently.
        if (hero != null && hero.IsPrisoner)
        {
            if (includeCaptivity)
            {
                TryStep(actions, "captivity release", () =>
                {
                    // Read before the release clears it.
                    var captor = hero.PartyBelongedToAsPrisoner;
                    var captorId = captor?.MobileParty?.StringId ?? captor?.Settlement?.StringId ?? "<unknown captor>";

                    // Patched: a registered player hero routes through the coop release flow, which also
                    // restores the deactivated player party.
                    EndCaptivityAction.ApplyByEscape(hero);
                    return $"Applied captivity release (escape) for hero {hero.StringId} from {captorId}.";
                });
            }
            else
            {
                actions.Add($"Left hero {hero.StringId} in captivity.");
            }
        }

        var army = party.Army;
        if (army?.LeaderParty == party)
        {
            actions.Add("Preserved the player-led army and its attached parties.");
        }
        else if (army != null)
        {
            TryStep(actions, "army removal", () =>
            {
                // Same pair the army-removal network flow uses: the publish makes ArmyHandler
                // broadcast NetworkRemovePartyInArmy to clients, the port applies it here.
                messageBroker.Publish(party, new MobilePartyInArmyRemoved(army, party, null));
                ArmyPatches.RemoveMobilePartyInArmy(party, army, null);
                return $"Removed the party from the army of {army.LeaderParty?.StringId ?? "<unknown leader>"}.";
            });
        }

        if (party.BesiegerCamp != null)
        {
            TryStep(actions, "siege camp removal", () =>
            {
                // Same application the break-siege request handler uses on the server.
                siegeEventInterface.BreakSiege(party);
                return "Removed the party from its siege camp.";
            });
        }

        var settlement = party.CurrentSettlement;
        if (settlement != null)
        {
            TryStep(actions, "settlement exit", () =>
            {
                // Patches live: the leave prefix publishes the attempt, which the settlement exit
                // handler broadcasts to clients while the native leave applies here.
                settlementInterface.PartyLeaveSettlement(party);
                return $"Removed the party from settlement {settlement.StringId}.";
            });
        }

        // Nothing ends a battle again once it ended, so parties it kept linked never move. Release them
        // all, lords and garrisons too, since they can block a town for everyone.
        TryStep(actions, "ended battle release", () =>
        {
            // Broadcast as each party is released, so a later group that throws can't swallow it.
            var releasedParties = mapEventLoadCleaner.ReleaseEndedMapEventLinks(releasedParty =>
            {
                if (objectManager.TryGetIdWithLogging(releasedParty, out var releasedPartyId))
                    network.SendAll(new NetworkPartyLeftBattle(releasedPartyId, finishLocalMenus: false));
            });

            return releasedParties.Length == 0
                ? null
                : $"Released {releasedParties.Length} parties still linked to a battle that already ended.";
        });

        if (party.Party?.MapEvent != null)
        {
            TryStep(actions, "map event removal", () =>
            {
                // The party-side setter also releases attached members on the server and clients.
                messageBroker.Publish(party, new PlayerLeaveBattleAttempted(party.Party));
                if (party.Party.MapEvent != null)
                    throw new InvalidOperationException("The battle leave flow did not remove the party from its map event.");

                return "Removed the party from its map event.";
            });
        }

        // After the player left, so a battle that only the player kept waiting can end too.
        TryStep(actions, "stalled battle resolve", () =>
        {
            var resolved = stalledMapEventResolver.ResolveStalledMapEvents();
            return resolved == 0
                ? null
                : $"Ended {resolved} battles left with no healthy troops on one side.";
        });

        // Last, so a party the exits above just freed is checked at its final spot.
        TryStep(actions, "map position repair", () =>
            partyNavigationRepair.TryRepair(party, out var previousPosition)
                ? $"Moved the party from {Format(previousPosition)}, a map spot it could not move from, to {Format(party.Position)}."
                : null);

        if (actions.Count == 0)
        {
            actions.Add("No server-side stuck state found (captivity, map event, army, siege camp, settlement, map position).");
        }

        return SendResult(player, partyId, actions);
    }

    private static string Format(CampaignVec2 position) =>
        string.Format(CultureInfo.InvariantCulture, "{0:0.##},{1:0.##}", position.X, position.Y);

    private string[] SendResult(Player player, string partyId, List<string> actions)
    {
        var result = actions.ToArray();
        if (playerManager.TryGetPeer(player.ControllerId, out var peer))
        {
            network.Send(peer, new NetworkPlayerUnstuckResult(partyId, result));
        }

        return result;
    }

    /// <summary>
    /// The registered player's hero, else the party leader. A captured player's party can have no
    /// leader, so the registration comes first.
    /// </summary>
    private Hero ResolvePlayerHero(Player player, MobileParty party)
    {
        if (!string.IsNullOrEmpty(player.HeroId) && objectManager.TryGetObject(player.HeroId, out Hero playerHero)) return playerHero;

        return party.LeaderHero;
    }

    private static void TryStep(List<string> actions, string step, Func<string> apply)
    {
        try
        {
            // A step returning null had nothing to apply.
            var line = apply();
            if (line != null) actions.Add(line);
        }
        catch (Exception e)
        {
            Logger.Error(e, "Unstuck step {Step} failed", step);
            actions.Add($"Failed {step}: {e.GetType().Name} ({e.Message}).");
        }
    }
}
