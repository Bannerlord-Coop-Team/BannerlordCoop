using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Hideouts;
using GameInterface.Services.MapEvents.Extensions;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.SiegeEvents.Messages;
using LiteNetLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.SiegeEvents.Handlers;

/// <summary>
/// [Server] Runs the keep (lord's hall) stage of a player-led siege assault: opens it once every walls member reported
/// the defenders falling back, answers each player's place and squad, and locks the roster on the selection window.
/// </summary>
internal sealed class SiegeKeepStageHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<SiegeKeepStageHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly ISiegeKeepStage stage;

    public SiegeKeepStageHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager,
        IPlayerManager playerManager, ISiegeKeepStage stage)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.stage = stage;
        messageBroker.Subscribe<AuthoritativeSiegeKeepRequested>(Handle_AuthoritativeSiegeKeepRequested);
        messageBroker.Subscribe<NetworkSiegeKeepEntryRequest>(Handle_EntryRequest);
        messageBroker.Subscribe<CampaignTick>(Handle_CampaignTick);
        messageBroker.Subscribe<MapEventFinalized>(Handle_MapEventFinalized);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<AuthoritativeSiegeKeepRequested>(Handle_AuthoritativeSiegeKeepRequested);
        messageBroker.Unsubscribe<NetworkSiegeKeepEntryRequest>(Handle_EntryRequest);
        messageBroker.Unsubscribe<CampaignTick>(Handle_CampaignTick);
        messageBroker.Unsubscribe<MapEventFinalized>(Handle_MapEventFinalized);
    }

    private void Handle_AuthoritativeSiegeKeepRequested(MessagePayload<AuthoritativeSiegeKeepRequested> payload)
    {
        var request = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MapEvent>(request.MapEventId, out var mapEvent))
            {
                // Otherwise the walls conclusion waits out its whole deadline before it is cleaned up.
                messageBroker.Publish(this, new BattleStateChangeProcessed(
                    request.MapEventId, BattleState.DefenderPullBack, false));
                return;
            }

            if (TryOpenStage(mapEvent))
            {
                messageBroker.Publish(this, new SiegeKeepStageOpened(request.MapEventId));
                return;
            }

            // Nobody can lead the keep, so the walls end the way an AI-led assault does.
            Logger.Information("Keep stage for {MapEventId} did not open; concluding the walls as an attacker victory",
                request.MapEventId);
            messageBroker.Publish(this, new AuthoritativeBattleConclusionRequested(
                request.MapEventId, BattleState.AttackerVictory, request.HostEpoch));
        }, context: nameof(Handle_AuthoritativeSiegeKeepRequested));
    }

    // A keep that throws while opening ends the walls instead of retrying, otherwise the same throw comes back each retry.
    private bool TryOpenStage(MapEvent mapEvent)
    {
        try
        {
            return stage.TryOpen(mapEvent);
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "Unable to open the keep stage for {MapEventId}", mapEvent.StringId);
            return false;
        }
    }

    private void Handle_EntryRequest(MessagePayload<NetworkSiegeKeepEntryRequest> payload)
    {
        if (payload.Who is not NetPeer peer) return;

        var request = payload.What;
        GameThread.RunSafe(() =>
        {
            NetworkSiegeKeepEntryReply reply;
            try
            {
                reply = Admit(peer, request);
            }
            catch (Exception exception)
            {
                Logger.Error(exception, "Unable to answer the keep request for {MapEventId}", request.MapEventId);
                reply = Reject(request, "Unable to prepare the keep battle.");
            }
            network.Send(peer, reply);
        }, context: nameof(Handle_EntryRequest));
    }

    private NetworkSiegeKeepEntryReply Admit(NetPeer peer, NetworkSiegeKeepEntryRequest request)
    {
        if (!playerManager.TryGetPlayer(peer, out var player) ||
            !objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party) ||
            !objectManager.TryGetObject<MapEvent>(request.MapEventId, out var mapEvent))
            return Reject(request, "This siege assault is no longer open.");

        bool assaultOpen = mapEvent.IsRunningSiegeAssault() && ReferenceEquals(party.MapEvent, mapEvent);
        if (!assaultOpen) return Reject(request, "This siege assault is no longer open.");

        if (!stage.IsOpen(mapEvent))
        {
            // A later assault on a siege that already reached the hall goes straight to the keep.
            bool opened = mapEvent.IsSiegeKeepStage() && stage.TryOpen(mapEvent);
            if (!opened) return Reject(request, "Nobody can lead the keep battle right now.");
        }

        if (!stage.TryGetSeat(mapEvent, player.MobilePartyId, out var seat))
        {
            if (stage.IsLocked(mapEvent))
                return Reject(request, "The keep battle has started without you. You will see the result.");
            return Reject(request, "The keep has no place left for you. You will see the result.");
        }

        if (stage.IsLocked(mapEvent))
            return Accept(request, seat, secondsRemaining: 0, isLocked: true);

        var seconds = stage.SecondsRemaining(mapEvent);
        if (request.QueryOnly)
            return Accept(request, seat, seconds, isLocked: false);

        if (!stage.TrySelect(mapEvent, party, request.Troops ?? Array.Empty<HideoutTroopSelectionEntry>()))
            return Reject(request, "That troop selection is no longer available.");

        return Accept(request, seat, seconds, stage.IsLocked(mapEvent));
    }

    private static NetworkSiegeKeepEntryReply Accept(NetworkSiegeKeepEntryRequest request, SiegeKeepSeat seat,
        int secondsRemaining, bool isLocked) =>
        new(request.RequestId, accepted: true, reason: null, escortAllowance: seat.EscortAllowance,
            secondsRemaining: secondsRemaining, isLocked: isLocked);

    private static NetworkSiegeKeepEntryReply Reject(NetworkSiegeKeepEntryRequest request, string reason) =>
        new(request.RequestId, accepted: false, reason: reason, escortAllowance: 0, secondsRemaining: 0);

    private void Handle_CampaignTick(MessagePayload<CampaignTick> payload)
    {
        if (ModInformation.IsClient) return;
        stage.Tick();
    }

    private void Handle_MapEventFinalized(MessagePayload<MapEventFinalized> payload) =>
        stage.Forget(payload.What.MapEvent);
}
