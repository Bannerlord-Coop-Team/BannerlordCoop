using Common.Messaging;
using Common.PacketHandlers;
using Coop.Core.Client.Messages;
using Coop.Core.Common.Network.Packets;
using GameInterface.Services.Time.Interfaces;
using LiteNetLib;
using System;

namespace Coop.Core.Client.Services.Time.Handlers;

/// <summary>
/// Receives the latest authoritative campaign time without waiting behind ordered world updates.
/// </summary>
public class CampaignTimePacketHandler : IPacketHandler
{
    private const float MaximumOneWayLatencySeconds = 0.25f;
    private const float LatencySmoothingRatio = 0.25f;

    public PacketType PacketType => PacketType.CampaignTime;

    private readonly IMessageBroker messageBroker;
    private readonly IPacketManager packetManager;
    private readonly IMapTimeTrackerInterface mapTimeTrackerInterface;
    private bool hasOneWayLatencyEstimate;
    private float oneWayLatencySeconds;

    public CampaignTimePacketHandler(
        IMessageBroker messageBroker,
        IPacketManager packetManager,
        IMapTimeTrackerInterface mapTimeTrackerInterface)
    {
        this.messageBroker = messageBroker;
        this.packetManager = packetManager;
        this.mapTimeTrackerInterface = mapTimeTrackerInterface;
        this.packetManager.RegisterPacketHandler(this);
        this.messageBroker.Subscribe<NetworkConnected>(Handle_NetworkConnected);
    }

    public void Dispose()
    {
        packetManager.RemovePacketHandler(this);
        messageBroker.Unsubscribe<NetworkConnected>(Handle_NetworkConnected);
    }

    public void HandlePacket(NetPeer peer, IPacket packet)
    {
        var timePacket = (CampaignTimePacket)packet;
        UpdateOneWayLatencyEstimate(peer);
        mapTimeTrackerInterface.SyncCampaignTime(timePacket.ServerTicks, oneWayLatencySeconds);
        messageBroker.Publish(
            this,
            new CampaignTimeSampleReceived(timePacket.JoinPacketsRemaining));
    }

    private void Handle_NetworkConnected(MessagePayload<NetworkConnected> obj)
    {
        hasOneWayLatencyEstimate = false;
        oneWayLatencySeconds = 0f;
    }

    internal static float CalculateOneWayLatencySeconds(int latencyMilliseconds)
    {
        return Math.Min(
            Math.Max(latencyMilliseconds, 0) / 1000f,
            MaximumOneWayLatencySeconds);
    }

    private void UpdateOneWayLatencyEstimate(NetPeer peer)
    {
        if (peer == null || peer.Ping <= 0) return;

        float measuredOneWayLatencySeconds = CalculateOneWayLatencySeconds(peer.Ping);
        if (hasOneWayLatencyEstimate)
        {
            oneWayLatencySeconds +=
                (measuredOneWayLatencySeconds - oneWayLatencySeconds) * LatencySmoothingRatio;
            return;
        }

        oneWayLatencySeconds = measuredOneWayLatencySeconds;
        hasOneWayLatencyEstimate = true;
    }
}
