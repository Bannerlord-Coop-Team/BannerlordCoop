using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using LiteNetLib;
using Serilog;
using System;

namespace GameInterface.Services.GameDebug.Metrics;

internal class PartySyncPerformanceHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartySyncPerformanceHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IPartySyncPerformancePartyProvider partyProvider;
    private readonly IPartySyncPerformanceLogger logger;

    public PartySyncPerformanceHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IPartySyncPerformancePartyProvider partyProvider,
        IPartySyncPerformanceLogger logger)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.partyProvider = partyProvider;
        this.logger = logger;

        messageBroker.Subscribe<NetworkRequestPartySyncPerformanceSnapshot>(Handle_Request);
        messageBroker.Subscribe<NetworkPartySyncPerformanceSnapshot>(Handle_Response);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkRequestPartySyncPerformanceSnapshot>(Handle_Request);
        messageBroker.Unsubscribe<NetworkPartySyncPerformanceSnapshot>(Handle_Response);
    }

    internal void Handle_Request(MessagePayload<NetworkRequestPartySyncPerformanceSnapshot> payload)
    {
        if (!ModInformation.IsServer) return;
        if (payload.Who is not NetPeer peer) return;

        int requestId = payload.What.RequestId;

        if (GameThread.Instance.IsInitialized && !GameThread.Instance.IsGameThread)
        {
            try
            {
                GameThread.RunSafe(() => SendSnapshot(peer, requestId), blocking: true, context: "PartySyncPerformanceHandler.Handle_Request");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to send party sync performance snapshot");
            }
            return;
        }

        SendSnapshot(peer, requestId);
    }

    private void SendSnapshot(NetPeer peer, int requestId)
    {
        try
        {
            network.Send(peer, new NetworkPartySyncPerformanceSnapshot(requestId, partyProvider.GetPartyData()));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to send party sync performance snapshot");
        }
    }

    internal void Handle_Response(MessagePayload<NetworkPartySyncPerformanceSnapshot> payload)
    {
        if (!ModInformation.IsClient) return;

        logger.HandleSnapshot(payload.What);
    }
}
