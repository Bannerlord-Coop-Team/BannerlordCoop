using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.Armies.Patches;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
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
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace GameInterface.Services.MobileParties;

/// <summary>
/// Server steps of the unstuck flow, shared by a player's own coop.unstuck request and an
/// operator's coop.unstuck on the server.
/// </summary>
internal interface IServerPlayerUnstuck
{
    /// <summary>
    /// Force-applies every applicable exit for the player's registered party and hero, then resyncs
    /// the map position of a party left free on the map, each step guarded independently, and sends
    /// the result to the player's connection when it has one. Runs on the game thread.
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

        // Last: only a party the exits above left free on the map has a map position to resync.
        if (!party.IsActive || party.CurrentSettlement != null || party.Party?.MapEvent != null ||
            party.BesiegerCamp != null || party.AttachedTo != null)
        {
            actions.Add("Left the map position alone because the party is not free on the map " +
                "(inactive, captive, or still in a settlement, battle, siege camp or army).");
        }
        else
        {
            TryStep(actions, "map position resync", () => ResyncMapPosition(party));
        }

        return SendResult(player, partyId, actions);
    }

    /// <summary>
    /// Puts the server copy of a free party back into a state it can move from and makes every client
    /// show it where the server has it. The server never takes a position from the client: a party off
    /// the navigation mesh (where native movement never advances it) goes to the nearest settlement
    /// gate, otherwise it stays where the server has it. Stale movement, path and land/sea transition
    /// state is cleared and the party is held, so the player's next order starts from a clean state.
    /// </summary>
    private string ResyncMapPosition(MobileParty party)
    {
        var before = party.Position;
        var moveMode = party.PartyMoveMode;
        var target = party.TargetSettlement?.StringId ?? party.TargetParty?.StringId ?? "none";
        var repairs = new List<string>();

        if (!party.CurrentNavigationFace.IsValid())
        {
            var gate = FindNearestGateOnNavigationMesh(before) ??
                throw new InvalidOperationException(
                    $"The party is off the navigation mesh at {Format(before)} and no settlement gate is on it.");
            party.Position = gate.GatePosition;
            repairs.Add($"moved it off an invalid map spot to the {gate.StringId} gate");
        }

        if (party.IsTransitionInProgress)
        {
            party.CancelNavigationTransition();
            repairs.Add("cancelled its land and sea transition");
        }

        if (party.Position.IsOnLand == party.IsCurrentlyAtSea)
        {
            party.ChangeIsCurrentlyAtSeaCheat();
            repairs.Add(party.IsCurrentlyAtSea ? "set it at sea" : "set it on land");
        }

        // Same hold reset and forced sync as coop.debug.mobile_party.restore_position.
        party.SetMoveModeHold();
        party.ResetNavigationToHold();
        party._pathMode = false;
        party._aiPathNotFound = false;
        party.PathLastFace = PathFaceRecord.NullFaceRecord;
        party.PathBegin = 0;
        messageBroker.Publish(this, new PartyBehaviorChangeAttempted(
            party,
            forcePosition: true,
            isCurrentlyAtSea: party.IsCurrentlyAtSea,
            resetMovementToHold: true));

        Logger.Information(
            "Unstuck resynced party {PartyId} from {Before} (moveMode {MoveMode}, target {Target}) to {After}; repairs: {Repairs}",
            party.StringId, Format(before), moveMode, target, Format(party.Position),
            repairs.Count == 0 ? "none" : string.Join(", ", repairs));

        var repaired = repairs.Count == 0 ? string.Empty : $"; {string.Join(", ", repairs)}";
        return $"Resynced the map position for every player to {Format(party.Position)} " +
            $"(server copy was at {Format(before)}, move mode {moveMode}, target {target}{repaired}) " +
            "and stopped the party so it takes new orders.";
    }

    private static Settlement FindNearestGateOnNavigationMesh(CampaignVec2 position)
    {
        Settlement nearest = null;
        var nearestDistance = float.MaxValue;
        foreach (var settlement in Settlement.All)
        {
            if (settlement.IsHideout || !settlement.GatePosition.Face.IsValid()) continue;

            var distance = settlement.GatePosition.Distance(position);
            if (distance >= nearestDistance) continue;

            nearest = settlement;
            nearestDistance = distance;
        }

        return nearest;
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
            actions.Add(apply());
        }
        catch (Exception e)
        {
            Logger.Error(e, "Unstuck step {Step} failed", step);
            actions.Add($"Failed {step}: {e.GetType().Name} ({e.Message}).");
        }
    }
}
