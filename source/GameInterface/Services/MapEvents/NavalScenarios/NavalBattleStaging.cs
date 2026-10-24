#if DEBUG
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Villages.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.NavalScenarios;

/// <summary>
/// [Game thread, server] DEBUG: the campaign moves that put players into a naval battle with an AI fleet, shared by
/// start_naval and the scenario stager.
/// </summary>
public interface INavalBattleStaging
{
    /// <summary>The player's party, once its client is ready; false and why otherwise.</summary>
    bool TryGetReadyPlayerParty(string controllerId, out MobileParty party, out string error);

    /// <summary>The ready players' parties, each of which must own a ship.</summary>
    bool TryResolvePlayerFleets(IReadOnlyList<string> controllerIds, out MobileParty[] playerParties, out string error);

    /// <summary>The hostile AI party with ships at sea nearest to <paramref name="playerParty"/>, or null.</summary>
    MobileParty FindNavalEnemy(MobileParty playerParty);

    /// <summary>Whether <paramref name="party"/> is a free AI party with ships at sea, at war with the player's faction.</summary>
    bool IsNavalEnemy(MobileParty party, MobileParty playerParty);

    /// <summary>Moves <paramref name="party"/> to <paramref name="position"/> at sea and holds it there, on every machine.</summary>
    void PlaceAtSea(MobileParty party, CampaignVec2 position);

    /// <summary>Moves every player party onto <paramref name="enemyParty"/>, which attacks the first one; the others join its side.</summary>
    bool TryStart(IReadOnlyList<MobileParty> playerParties, MobileParty enemyParty, out MapEvent mapEvent, out string mapEventId,
        out string error);

    /// <summary>Adds <paramref name="allyParty"/> to <paramref name="playerParty"/>'s side and opens the ally's encounter menu.</summary>
    bool TryJoinNavalAlly(string mapEventId, MobileParty enemyParty, MobileParty playerParty, MobileParty allyParty, out string error);
}

/// <inheritdoc cref="INavalBattleStaging"/>
public class NavalBattleStaging : INavalBattleStaging
{
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IMessageBroker messageBroker;

    public NavalBattleStaging(IObjectManager objectManager, INetwork network, IMessageBroker messageBroker)
    {
        this.objectManager = objectManager;
        this.network = network;
        this.messageBroker = messageBroker;
    }

    public bool TryGetReadyPlayerParty(string controllerId, out MobileParty party, out string error) =>
        MapEventDebugCommands.TryGetPlayerParty(controllerId, requireReady: true, out _, out party, out error);

    public bool TryResolvePlayerFleets(IReadOnlyList<string> controllerIds, out MobileParty[] playerParties, out string error)
    {
        playerParties = new MobileParty[controllerIds.Count];
        for (int i = 0; i < controllerIds.Count; i++)
        {
            if (!TryGetReadyPlayerParty(controllerIds[i], out playerParties[i], out error)) return false;

            if (playerParties[i].Ships.Count == 0)
            {
                error = $"Player {controllerIds[i]} has no ships.";
                return false;
            }
        }

        error = null;
        return true;
    }

    public MobileParty FindNavalEnemy(MobileParty playerParty)
    {
        var playerPosition = playerParty.Position.ToVec2();
        return MobileParty.All
            .Where(party => IsNavalEnemy(party, playerParty))
            .OrderBy(party => party.Position.ToVec2().DistanceSquared(playerPosition))
            .FirstOrDefault();
    }

    public bool TryStart(IReadOnlyList<MobileParty> playerParties, MobileParty enemyParty, out MapEvent mapEvent,
        out string mapEventId, out string error)
    {
        mapEventId = null;
        foreach (var playerParty in playerParties)
            PlaceAtSea(playerParty, enemyParty.Position);

        StartBattleAction.Apply(enemyParty.Party, playerParties[0].Party);
        mapEvent = playerParties[0].MapEvent;
        if (mapEvent == null)
        {
            error = $"StartBattleAction did not create a map event against {enemyParty.StringId}.";
            return false;
        }

        if (!objectManager.TryGetId(mapEvent, out mapEventId))
        {
            error = "The naval map event is not registered.";
            return false;
        }

        for (int i = 1; i < playerParties.Count; i++)
        {
            if (!TryJoinNavalAlly(mapEventId, enemyParty, playerParties[0], playerParties[i], out error)) return false;
        }

        error = null;
        return true;
    }

    public bool IsNavalEnemy(MobileParty party, MobileParty playerParty) =>
        party.IsActive &&
        !party.IsPlayerParty() &&
        party.IsCurrentlyAtSea &&
        party.Ships.Count > 0 &&
        party.MapEvent == null &&
        party.CurrentSettlement == null &&
        party.MemberRoster.TotalManCount > 0 &&
        party.MapFaction != null &&
        FactionManager.IsAtWarAgainstFaction(party.MapFaction, playerParty.MapFaction);

    public void PlaceAtSea(MobileParty party, CampaignVec2 position)
    {
        if (party.CurrentSettlement != null)
            LeaveSettlementAction.ApplyForParty(party);

        party.Position = position;
        party.IsCurrentlyAtSea = true;
        party.SetMoveModeHold();
        party.ResetNavigationToHold();
        messageBroker.Publish(this, new PartyBehaviorChangeAttempted(
            party,
            forcePosition: true,
            isCurrentlyAtSea: true,
            resetMovementToHold: true));
    }

    public bool TryJoinNavalAlly(string mapEventId, MobileParty enemyParty, MobileParty playerParty, MobileParty allyParty,
        out string error)
    {
        error = null;
        allyParty.Party.MapEventSide = playerParty.Party.MapEventSide;
        if (allyParty.MapEvent != playerParty.MapEvent)
        {
            error = "The ally party was not added to the naval map event.";
            return false;
        }

        if (!objectManager.TryGetId(enemyParty.Party, out string enemyPartyId) ||
            !objectManager.TryGetId(allyParty.Party, out string allyPartyId))
        {
            error = "Unable to resolve the naval ally encounter ids.";
            return false;
        }

        // Opens the ally's encounter menu, the same way battle_reward_fixture_join does for its late joiner.
        network.SendAll(new NetworkPlayerPartyHostileEncounterStarted(
            $"debug-naval-join-{Guid.NewGuid():N}",
            enemyPartyId,
            allyPartyId,
            mapEventId));
        return true;
    }
}
#endif
