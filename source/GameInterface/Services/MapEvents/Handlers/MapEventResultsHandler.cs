using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MapEvents.Data;
using GameInterface.Services.MapEvents.Interfaces;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEventParties;
using GameInterface.Services.MapEventParties.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Handlers;

internal class MapEventResultsHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<MapEventResultsHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IMapEventResultsInterface mapEventResultsInterface;
    private readonly IMapEventContributionBarrier contributionBarrier;

    public MapEventResultsHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IMapEventResultsInterface mapEventResultsInterface,
        IMapEventContributionBarrier contributionBarrier)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.mapEventResultsInterface = mapEventResultsInterface;
        this.contributionBarrier = contributionBarrier;

        messageBroker.Subscribe<CommitMapEventResults>(Handle_CommitMapEventResults);
        messageBroker.Subscribe<NetworkCommitMapEventResults>(Handle_NetworkCommitMapEventResults);
        messageBroker.Subscribe<MapEventContributionFlushRequested>(Handle_MapEventContributionFlushRequested);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<CommitMapEventResults>(Handle_CommitMapEventResults);
        messageBroker.Unsubscribe<NetworkCommitMapEventResults>(Handle_NetworkCommitMapEventResults);
        messageBroker.Unsubscribe<MapEventContributionFlushRequested>(Handle_MapEventContributionFlushRequested);
    }

    private void Handle_MapEventContributionFlushRequested(
        MessagePayload<MapEventContributionFlushRequested> payload)
    {
        if (ModInformation.IsClient) return;

        // Keep this inline so the publishing patch cannot continue into result or teardown before the flush.
        if (payload.What.MapEventParty != null)
            contributionBarrier.Flush(payload.What.MapEventParty);
        else
            contributionBarrier.Flush(payload.What.MapEvent);
    }

    private void Handle_CommitMapEventResults(MessagePayload<CommitMapEventResults> obj)
    {
        var mapEvent = obj.What.MapEvent;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(mapEvent, out var mapEventId)) return;

            contributionBarrier.Flush(mapEvent);
            mapEventResultsInterface.CalculateAndCommitMapEventResults(mapEvent, out NetworkPlayerLootData networkPlayerLootData);

            var message = new NetworkCommitMapEventResults(mapEventId, mapEvent.WinningSide, networkPlayerLootData);
            network.SendAll(message);
        });
    }

    private void Handle_NetworkCommitMapEventResults(MessagePayload<NetworkCommitMapEventResults> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MapEvent>(data.MapEventId, out var mapEvent)) return;

            // Only stage the encounter of a client whose own party fought this battle; an uninvolved client
            // (no open encounter, or one for something unrelated — a town visit, a conversation) must not
            // have its encounter state touched by another battle's results.
            var playerEncounter = PlayerEncounter.Current;
            if (playerEncounter == null) return;

            var mainParty = PartyBase.MainParty;
            if (mainParty?.MapEventSide?.MapEvent != mapEvent) return;

            var playerLootData = mapEventResultsInterface.UnpackPlayerLootData(data.PlayerLootData);

            // Set the encounter state ahead to start at applying results when a winning player leaves the battle
            // CaptureHeroes is the first EncounterState that doesn't rely on the MapEvent, which is already destroyed when a player leaves a battle
            if (data.WinningSide == mainParty.Side)
            {
                playerEncounter.EncounterState = PlayerEncounterState.CaptureHeroes;
            }
            else // Player defeat handled elsewhere, this only cares about player victories for giving loot to players
            {
                playerEncounter.EncounterState = PlayerEncounterState.End;
            }

            using (new AllowedThread())
            {
                // Add looted items to player encounter
                foreach (var playerLootedItems in playerLootData.LootedItems)
                {
                    if (playerLootedItems.Key.Party != mainParty) continue;

                    playerEncounter.RosterToReceiveLootItems.Add(playerLootedItems.Value);
                }

                // Add looted members to player encounter
                foreach (var playerLootedMembers in playerLootData.LootedMembers)
                {
                    if (playerLootedMembers.Key.Party != mainParty) continue;

                    playerEncounter.RosterToReceiveLootMembers.Add(playerLootedMembers.Value);
                }

                // Add looted prisoners to player encounter
                foreach (var playerLootedPrisoners in playerLootData.LootedPrisoners)
                {
                    if (playerLootedPrisoners.Key.Party != mainParty) continue;

                    playerEncounter.RosterToReceiveLootPrisoners.Add(playerLootedPrisoners.Value);
                }
            }
        });
    }
}
