using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.Armies.Patches;
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
    /// Force-applies every applicable exit for the player's registered party and hero, each step
    /// guarded independently, and sends the result to the player's connection when it has one.
    /// Runs on the game thread.
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

    public ServerPlayerUnstuck(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        ISettlementInterface settlementInterface,
        ISiegeEventInterface siegeEventInterface)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.settlementInterface = settlementInterface;
        this.siegeEventInterface = siegeEventInterface;
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

        if (actions.Count == 0)
        {
            actions.Add("No server-side stuck state found (captivity, map event, army, siege camp, settlement).");
        }

        return SendResult(player, partyId, actions);
    }

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
            actions.Add(apply());
        }
        catch (Exception e)
        {
            Logger.Error(e, "Unstuck step {Step} failed", step);
            actions.Add($"Failed {step}: {e.GetType().Name} ({e.Message}).");
        }
    }
}
