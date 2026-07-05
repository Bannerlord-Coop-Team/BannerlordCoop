using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEvents.Data;
using GameInterface.Services.MapEvents.Interfaces;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.MapEvents.Handlers;

internal class MapEventResultsHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<MapEventResultsHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IMapEventResultsInterface mapEventResultsInterface;

    public MapEventResultsHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IMapEventResultsInterface mapEventResultsInterface)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.mapEventResultsInterface = mapEventResultsInterface;

        messageBroker.Subscribe<CommitMapEventResults>(Handle_CommitMapEventResults);
        messageBroker.Subscribe<NetworkCommitMapEventResults>(Handle_NetworkCommitMapEventResults);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<CommitMapEventResults>(Handle_CommitMapEventResults);
        messageBroker.Unsubscribe<NetworkCommitMapEventResults>(Handle_NetworkCommitMapEventResults);
    }

    private void Handle_CommitMapEventResults(MessagePayload<CommitMapEventResults> obj)
    {
        var mapEvent = obj.What.MapEvent;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(mapEvent, out var mapEventId)) return;

            mapEventResultsInterface.CalculateAndCommitMapEventResults(mapEvent, out NetworkPlayerLootData networkPlayerLootData);

            var message = new NetworkCommitMapEventResults(mapEventId, networkPlayerLootData);
            network.SendAll(message);
        });
    }

    private void Handle_NetworkCommitMapEventResults(MessagePayload<NetworkCommitMapEventResults> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MapEvent>(data.MapEventId, out var mapEvent)) return;
            var playerLootData = mapEventResultsInterface.UnpackPlayerLootData(data.PlayerLootData);

            var playerEncounter = PlayerEncounter.Current;
            if (mapEvent.WinningSide == PartyBase.MainParty.Side)
            {
                playerEncounter.EncounterState = PlayerEncounterState.CaptureHeroes;
            }
            else // Player defeat handled elsewhere, this only cares about player victories for giving loot to players
            {
                playerEncounter.EncounterState = PlayerEncounterState.End;
            }

            // Add looted items to player encounter
            foreach (var playerLootedItems in playerLootData.LootedItems)
            {
                if (playerLootedItems.Key.Party != PartyBase.MainParty) continue;

                playerEncounter.RosterToReceiveLootItems.Add(playerLootedItems.Value);
            }

            // Add looted prisoners to player encounter
        });
    }
}
