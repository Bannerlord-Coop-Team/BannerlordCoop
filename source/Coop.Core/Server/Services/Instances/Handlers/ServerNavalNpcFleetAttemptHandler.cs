using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Ships;
using GameInterface.Services.Ships.Messages;
using LiteNetLib;
using Missions.Messages;
using Serilog;

namespace Coop.Core.Server.Services.Instances.Handlers;

/// <summary>
/// [Server] Records each naval battle side's AI fleet attempt its battle host reports and sends it to every client, and to a
/// client entering the campaign later, so a player who opens that battle afterwards caps its fleet against it.
/// </summary>
public class ServerNavalNpcFleetAttemptHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ServerNavalNpcFleetAttemptHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly INavalBattleMemberResolver memberResolver;
    private readonly INavalNpcFleetAttemptTracker attempts;

    // Takes the network the attempts go out on, the services that resolve the reporting host and the server's attempt tracker.
    public ServerNavalNpcFleetAttemptHandler(IMessageBroker messageBroker, INetwork network, IPlayerManager playerManager,
        INavalBattleMemberResolver memberResolver, INavalNpcFleetAttemptTracker attempts)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.playerManager = playerManager;
        this.memberResolver = memberResolver;
        this.attempts = attempts;

        messageBroker.Subscribe<NetworkNpcFleetAttempted>(Handle_NetworkNpcFleetAttempted);
        messageBroker.Subscribe<PlayerCampaignEntered>(Handle_PlayerCampaignEntered);
    }

    // Drops the handler's subscriptions.
    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkNpcFleetAttempted>(Handle_NetworkNpcFleetAttempted);
        messageBroker.Unsubscribe<PlayerCampaignEntered>(Handle_PlayerCampaignEntered);
    }

    // Resolves the battle host on the poll thread, as its disconnect in the same batch clears its peer before the game thread runs.
    private void Handle_NetworkNpcFleetAttempted(MessagePayload<NetworkNpcFleetAttempted> payload)
    {
        if (payload.Who is not NetPeer peer) return;

        var attempt = payload.What;
        if (!playerManager.TryGetCurrentController(peer, out var controllerId))
        {
            Logger.Warning("[NavalSync] Dropped an AI fleet attempt for {Instance}: the sender is not a registered player's current peer",
                attempt.InstanceId);
            return;
        }

        GameThread.RunSafe(() => Record(controllerId, attempt), context: nameof(Handle_NetworkNpcFleetAttempted));
    }

    // [Game thread] Records the side's first attempt of an open battle and sends it to every client, on the same ordered
    // channel as any later join, so a client always holds it before it can open that battle's mission.
    private void Record(string controllerId, NetworkNpcFleetAttempted reported)
    {
        if (!memberResolver.TryResolve(controllerId, reported.InstanceId, "an AI fleet attempt", out _, out _)) return;

        var attempt = new NetworkNpcFleetAttemptRecorded(reported.InstanceId, reported.Side, reported.AiHulls, reported.PlayerHulls,
            reported.CountedPartyIds);
        if (!attempts.Record(attempt)) return;

        network.SendAll(attempt);
        Logger.Information("[NavalBattle] Recorded the {Side} AI fleet attempt of {Instance} from {Controller}: failed={Failed} " +
            "aiHulls={AiHulls} playerHulls={PlayerHulls}", reported.Side, reported.InstanceId, controllerId, reported.Failed,
            reported.AiHulls, reported.PlayerHulls);
    }

    // [Game thread] A client entering the campaign missed the earlier sends, so it gets every open battle's attempts now.
    private void Handle_PlayerCampaignEntered(MessagePayload<PlayerCampaignEntered> payload)
    {
        foreach (var attempt in attempts.All)
            network.Send(payload.What.playerId, attempt);
    }
}
