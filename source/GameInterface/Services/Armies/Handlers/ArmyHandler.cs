using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.Armies.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Armies.Handlers;

/// <summary>
/// Handler for <see cref="Army"/> messages
/// </summary>
public class ArmyHandler : IHandler
{

    private static readonly ILogger Logger = LogManager.GetLogger<ArmyHandler>();
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    public ArmyHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;

        messageBroker.Subscribe<MobilePartyInArmyAdded>(HandleAddMobilePartyInArmy);
        messageBroker.Subscribe<NetworkAddMobilePartyInArmy>(HandleChangeAddMobilePartyInArmy);
        messageBroker.Subscribe<MobilePartyInArmyRemoved>(HandleRemoveMobilePartyInArmy);
        messageBroker.Subscribe<NetworkRemovePartyInArmy>(HandleChangeRemoveMobilePartyInArmy);
        messageBroker.Subscribe<ArmyAiBehaviorObjectChanged>(HandleArmyAiBehaviorObjectChanged);
        messageBroker.Subscribe<NetworkSetArmyAiBehaviorObject>(HandleNetworkSetArmyAiBehaviorObject);
        messageBroker.Subscribe<PlayerCreatedArmy>(HandlePlayerCreatedArmy);
        messageBroker.Subscribe<NetworkPlayerCreatedArmy>(HandleNetworkPlayerCreatedArmy);
        messageBroker.Subscribe<PlayerAddedPartiesToArmy>(HandlePlayerAddedPartiesToArmy);
        messageBroker.Subscribe<NetworkPlayerAddedPartiesToArmy>(HandleNetworkPlayerAddedPartiesToArmy);
        messageBroker.Subscribe<PlayerRemovedPartiesFromArmy>(HandlePlayerRemovedPartiesFromArmy);
        messageBroker.Subscribe<NetworkPlayerRemovedPartiesFromArmy>(HandleNetworkPlayerRemovedPartiesFromArmy);
        messageBroker.Subscribe<PlayerBoostedArmyCohesion>(HandlePlayerBoostedArmyCohesion);
        messageBroker.Subscribe<NetworkPlayerBoostedArmyCohesion>(HandleNetworkPlayerBoostedArmyCohesion);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<MobilePartyInArmyAdded>(HandleAddMobilePartyInArmy);
        messageBroker.Unsubscribe<NetworkAddMobilePartyInArmy>(HandleChangeAddMobilePartyInArmy);
        messageBroker.Unsubscribe<MobilePartyInArmyRemoved>(HandleRemoveMobilePartyInArmy);
        messageBroker.Unsubscribe<NetworkRemovePartyInArmy>(HandleChangeRemoveMobilePartyInArmy);
        messageBroker.Unsubscribe<ArmyAiBehaviorObjectChanged>(HandleArmyAiBehaviorObjectChanged);
        messageBroker.Unsubscribe<NetworkSetArmyAiBehaviorObject>(HandleNetworkSetArmyAiBehaviorObject);
        messageBroker.Unsubscribe<PlayerCreatedArmy>(HandlePlayerCreatedArmy);
        messageBroker.Unsubscribe<NetworkPlayerCreatedArmy>(HandleNetworkPlayerCreatedArmy);
        messageBroker.Unsubscribe<PlayerAddedPartiesToArmy>(HandlePlayerAddedPartiesToArmy);
        messageBroker.Unsubscribe<NetworkPlayerAddedPartiesToArmy>(HandleNetworkPlayerAddedPartiesToArmy);
        messageBroker.Unsubscribe<PlayerRemovedPartiesFromArmy>(HandlePlayerRemovedPartiesFromArmy);
        messageBroker.Unsubscribe<NetworkPlayerRemovedPartiesFromArmy>(HandleNetworkPlayerRemovedPartiesFromArmy);
        messageBroker.Unsubscribe<PlayerBoostedArmyCohesion>(HandlePlayerBoostedArmyCohesion);
        messageBroker.Unsubscribe<NetworkPlayerBoostedArmyCohesion>(HandleNetworkPlayerBoostedArmyCohesion);
    }

    private void HandleAddMobilePartyInArmy(MessagePayload<MobilePartyInArmyAdded> obj)
    {

        if (!objectManager.TryGetIdWithLogging(obj.What.Army, out var armyId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.MobileParty, out var mobilePartyId)) return;

        var message = new NetworkAddMobilePartyInArmy(armyId, mobilePartyId);

        // Broadcast to all the clients that the state was changed   
        network.SendAll(message);
    }

    private void HandleChangeAddMobilePartyInArmy(MessagePayload<NetworkAddMobilePartyInArmy> payload)
    {
        var obj = payload.What;

        if (objectManager.TryGetObjectWithLogging(obj.MobilePartyId, out MobileParty mobileParty) == false) return;
        if (objectManager.TryGetObjectWithLogging<Army>(obj.ArmyId, out var army) == false) return;

        ArmyPatches.AddMobilePartyInArmy(mobileParty, army);
    }

    private void HandleRemoveMobilePartyInArmy(MessagePayload<MobilePartyInArmyRemoved> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.Army, out var armyId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.MobileParty, out var mobilePartyId)) return;

        var message = new NetworkRemovePartyInArmy(armyId, mobilePartyId);

        // Broadcast to all the clients that the state was changed
        network.SendAll(message);
    }

    private void HandleChangeRemoveMobilePartyInArmy(MessagePayload<NetworkRemovePartyInArmy> payload)
    {
        var data = payload.What;

        if (objectManager.TryGetObjectWithLogging(data.MobilePartyId, out MobileParty mobileParty) == false) return;
        if (objectManager.TryGetObjectWithLogging<Army>(data.ArmyId, out var army) == false) return;

        ArmyPatches.RemoveMobilePartyInArmy(mobileParty, army);
    }

    private void HandleArmyAiBehaviorObjectChanged(MessagePayload<ArmyAiBehaviorObjectChanged> payload)
    {
        var obj = payload.What;
        if (!objectManager.TryGetIdWithLogging(obj.Army, out var armyId)) return;

        bool isSettlement = obj.AiBehaviorObject is Settlement;
        if (!objectManager.TryGetIdWithLogging(obj.AiBehaviorObject, out var objectId)) return;

        var message = new NetworkSetArmyAiBehaviorObject(armyId, objectId, isSettlement);

        // Broadcast to all the clients that the state was changed   
        network.SendAll(message);
    }

    private void HandleNetworkSetArmyAiBehaviorObject(MessagePayload<NetworkSetArmyAiBehaviorObject> payload)
    {
        var obj = payload.What;
        if (objectManager.TryGetObjectWithLogging<Army>(obj.ArmyId, out var army) == false) return;

        IMapPoint mapPoint;
        if (obj.IsSettlement)
        {
            if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.AiBehaviorObjectId, out var settlement)) return;
            mapPoint = settlement;
        }
        else
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.AiBehaviorObjectId, out var party)) return;
            mapPoint = party;
        }

        ArmyPatches.SetAiBehaviorObject(army, mapPoint);
    }
    private void HandlePlayerCreatedArmy(MessagePayload<PlayerCreatedArmy> payload)
    {
        var obj = payload.What;
        if (!objectManager.TryGetIdWithLogging(obj.Kingdom, out var kingdomId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.Leader, out var leaderId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.TargetSettlement, out var targetSettlementId)) return;

        var partyIds = new List<string>();
        foreach (var party in obj.Parties)
        {
            if (!objectManager.TryGetIdWithLogging(party, out var partyId)) return;
            partyIds.Add(partyId);
        }

        var message = new NetworkPlayerCreatedArmy(kingdomId, leaderId, targetSettlementId, obj.ArmyType.ToString(), partyIds);
        network.SendAll(message);
    }

    private void HandleNetworkPlayerCreatedArmy(MessagePayload<NetworkPlayerCreatedArmy> payload)
    {
        var obj = payload.What;
        if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.KingdomId, out var kingdom)) return;
        if (!objectManager.TryGetObjectWithLogging<Hero>(obj.LeaderId, out var leader)) return;
        if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.TargetSettlementId, out var targetSettlement)) return;

        var parties = new List<MobileParty>();
        foreach (var partyId in obj.PartyIds)
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(partyId, out var party)) return;
            parties.Add(party);
        }

        var armyType = (Army.ArmyTypes)Enum.Parse(typeof(Army.ArmyTypes), obj.ArmyTypeId);

        GameThread.RunSafe(() =>
        {
            try
            {
                // Only create if the leader doesn't already have an army
                if (leader.PartyBelongedTo?.Army == null)
                {
                    kingdom.CreateArmy(leader, targetSettlement, armyType);
                }

                var army = leader.PartyBelongedTo?.Army;
                if (army == null) return;
                // Assign each selected party to the new army
                foreach (var party in parties)
                {
                    party.Army = army;
                }
                // Refresh the map overlay so the army icon appears on the client
                var mapState = Game.Current.GameStateManager.GameStates
                    .OfType<MapState>()
                    .SingleOrDefault();
                mapState?.OnArmyCreated(leader.PartyBelongedTo);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "CreateArmy failed");
            }
        });
    }
    private void HandlePlayerAddedPartiesToArmy(MessagePayload<PlayerAddedPartiesToArmy> payload)
    {
        var obj = payload.What;
        if (!objectManager.TryGetIdWithLogging(obj.Army, out var armyId)) return;

        var partyIds = new List<string>();
        foreach (var party in obj.Parties)
        {
            if (!objectManager.TryGetIdWithLogging(party, out var partyId)) return;
            partyIds.Add(partyId);
        }

        network.SendAll(new NetworkPlayerAddedPartiesToArmy(armyId, partyIds));
    }

    private void HandleNetworkPlayerAddedPartiesToArmy(MessagePayload<NetworkPlayerAddedPartiesToArmy> payload)
    {
        var obj = payload.What;
        if (!objectManager.TryGetObjectWithLogging<Army>(obj.ArmyId, out var army)) return;

        var parties = new List<MobileParty>();
        foreach (var partyId in obj.PartyIds)
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(partyId, out var party)) return;
            parties.Add(party);
        }

        GameThread.RunSafe(() =>
        {
            try
            {
                foreach (var party in parties)
                {
                    party.Army = army;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to add parties to army");
            }
        });
    }
    private void HandlePlayerRemovedPartiesFromArmy(MessagePayload<PlayerRemovedPartiesFromArmy> payload)
    {
        var obj = payload.What;

        var partyIds = new List<string>();
        foreach (var party in obj.Parties)
        {
            if (!objectManager.TryGetIdWithLogging(party, out var partyId)) return;
            partyIds.Add(partyId);
        }

        network.SendAll(new NetworkPlayerRemovedPartiesFromArmy(partyIds));
    }

    private void HandleNetworkPlayerRemovedPartiesFromArmy(MessagePayload<NetworkPlayerRemovedPartiesFromArmy> payload)
    {
        var obj = payload.What;

        var parties = new List<MobileParty>();
        foreach (var partyId in obj.PartyIds)
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(partyId, out var party)) return;
            parties.Add(party);
        }

        GameThread.RunSafe(() =>
        {
            try
            {
                foreach (var party in parties)
                {
                    party.Army = null;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to remove parties from army");
            }
        });
    }
    private void HandlePlayerBoostedArmyCohesion(MessagePayload<PlayerBoostedArmyCohesion> payload)
    {
        var obj = payload.What;
        if (!objectManager.TryGetIdWithLogging(obj.ArmyLeaderParty, out var leaderPartyId)) return;

        network.SendAll(new NetworkPlayerBoostedArmyCohesion(leaderPartyId, obj.CohesionToGain, obj.InfluenceCost));
    }

    private void HandleNetworkPlayerBoostedArmyCohesion(MessagePayload<NetworkPlayerBoostedArmyCohesion> payload)
    {
        var obj = payload.What;
        if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.ArmyLeaderPartyId, out var leaderParty)) return;

        GameThread.RunSafe(() =>
        {
            try
            {
                if (leaderParty.Army == null) return;
                // BoostCohesionWithInfluence increments Army.Cohesion and deducts influence.
                // Do not deduct influence separately it is fully handled here
                leaderParty.Army.BoostCohesionWithInfluence(obj.CohesionToGain, obj.InfluenceCost);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to boost army cohesion");
            }
        });
    }
}