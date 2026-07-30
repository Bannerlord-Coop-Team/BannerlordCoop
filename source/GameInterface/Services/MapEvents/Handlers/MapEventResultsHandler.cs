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
using GameInterface.Services.Players;
using Serilog;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Handlers;

internal class MapEventResultsHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<MapEventResultsHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IMapEventResultsInterface mapEventResultsInterface;
    private readonly IMapEventContributionBarrier contributionBarrier;
    private readonly IPlayerManager playerManager;

    public MapEventResultsHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IMapEventResultsInterface mapEventResultsInterface,
        IMapEventContributionBarrier contributionBarrier,
        IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.mapEventResultsInterface = mapEventResultsInterface;
        this.contributionBarrier = contributionBarrier;
        this.playerManager = playerManager;

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

            foreach (var player in playerManager.Players)
            {
                if (!playerManager.TryGetPeer(player.ControllerId, out var peer) ||
                    !TryGetPlayerMapEventParty(mapEvent, player.MobilePartyId, out var playerMapEventParty, out var playerSide) ||
                    !objectManager.TryGetIdWithLogging(playerMapEventParty, out var playerMapEventPartyId))
                {
                    continue;
                }

                network.Send(peer, new NetworkCommitMapEventResults(
                    mapEventId,
                    mapEvent.WinningSide,
                    playerSide,
                    playerMapEventPartyId,
                    networkPlayerLootData));
            }
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
            if (playerEncounter == null || PlayerEncounter.Battle != mapEvent) return;

            if ((data.PlayerSide != BattleSideEnum.Attacker && data.PlayerSide != BattleSideEnum.Defender) ||
                string.IsNullOrEmpty(data.PlayerMapEventPartyId))
                return;

            mapEventResultsInterface.UnpackPlayerLootDataForParty(
                data.PlayerLootData,
                data.PlayerMapEventPartyId,
                out var lootedItems,
                out var lootedMembers,
                out var lootedPrisoners);

            // Set the encounter state ahead to start at applying results when a winning player leaves the battle
            // CaptureHeroes is the first EncounterState that doesn't rely on the MapEvent, which is already destroyed when a player leaves a battle
            if (data.WinningSide == data.PlayerSide)
            {
                playerEncounter.EncounterState = PlayerEncounterState.CaptureHeroes;
            }
            else // Player defeat handled elsewhere, this only cares about player victories for giving loot to players
            {
                playerEncounter.EncounterState = PlayerEncounterState.End;
            }

            using (new AllowedThread())
            {
                playerEncounter.RosterToReceiveLootItems.Add(lootedItems);
                playerEncounter.RosterToReceiveLootMembers.Add(lootedMembers);
                playerEncounter.RosterToReceiveLootPrisoners.Add(lootedPrisoners);
            }
        });
    }

    private bool TryGetPlayerMapEventParty(
        MapEvent mapEvent,
        string playerMobilePartyId,
        out MapEventParty playerMapEventParty,
        out BattleSideEnum playerSide)
    {
        playerMapEventParty = null;
        playerSide = BattleSideEnum.None;

        if (TryGetPlayerMapEventParty(mapEvent.AttackerSide, playerMobilePartyId, out playerMapEventParty))
        {
            playerSide = BattleSideEnum.Attacker;
            return true;
        }

        if (TryGetPlayerMapEventParty(mapEvent.DefenderSide, playerMobilePartyId, out playerMapEventParty))
        {
            playerSide = BattleSideEnum.Defender;
            return true;
        }

        return false;
    }

    private bool TryGetPlayerMapEventParty(
        MapEventSide mapEventSide,
        string playerMobilePartyId,
        out MapEventParty playerMapEventParty)
    {
        foreach (var mapEventParty in mapEventSide.Parties)
        {
            var mobileParty = mapEventParty.Party?.MobileParty;
            if (mobileParty == null ||
                !objectManager.TryGetId(mobileParty, out var mobilePartyId) ||
                mobilePartyId != playerMobilePartyId)
            {
                continue;
            }

            playerMapEventParty = mapEventParty;
            return true;
        }

        playerMapEventParty = null;
        return false;
    }
}
