using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using Coop.Core.Client.Services.MobileParties.Packets;
using Coop.Core.Server.Services.MobileParties.Packets;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Messages.Behavior;
using LiteNetLib;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Coop.Core.Server.Services.MobileParties.PacketHandlers;

/// <summary>
/// Handles incoming <see cref="RequestMobilePartyBehaviorPacket"/>
/// </summary>
internal class RequestMobilePartyBehaviorPacketHandler : IPacketHandler
{
    public PacketType PacketType => PacketType.RequestUpdatePartyBehavior;

    private readonly IPacketManager packetManager;
    private readonly INetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly Task poller;
    private readonly CancellationTokenSource cts = new CancellationTokenSource();

    private readonly ConcurrentDictionary<string, PartyBehaviorUpdateData> updateStore = new();

    public RequestMobilePartyBehaviorPacketHandler(
        IPacketManager packetManager,
        INetwork network,
        IMessageBroker messageBroker)
    {
        this.packetManager = packetManager;
        this.network = network;
        this.messageBroker = messageBroker;
        packetManager.RegisterPacketHandler(this);

        poller = Task.Factory.StartNew(Poll);

        messageBroker.Subscribe<PartyBehaviorUpdated>(Handle_PartyBehaviorUpdated);
    }

    public void Dispose()
    {
        packetManager.RemovePacketHandler(this);

        cts.Cancel();

        messageBroker.Unsubscribe<PartyBehaviorUpdated>(Handle_PartyBehaviorUpdated);
    }

    public void HandlePacket(NetPeer peer, IPacket packet)
    {
        RequestMobilePartyBehaviorPacket convertedPacket = (RequestMobilePartyBehaviorPacket)packet;

        var data = convertedPacket.BehaviorUpdateData;

        messageBroker.Publish(this, new UpdatePartyBehavior(ref data));
    }

    private void Handle_PartyBehaviorUpdated(MessagePayload<PartyBehaviorUpdated> payload)
    {
        var data = payload.What.BehaviorUpdateData;

        lock (updateStore)
        {
            updateStore.TryAdd(data.PartyId, data);
        }
    }

    private TimeSpan PollingInterval = TimeSpan.FromSeconds(1);
    private async void Poll()
    {
        while(cts.IsCancellationRequested == false)
        {
            await Task.Delay(PollingInterval);

            lock (updateStore)
            {
                var datas = updateStore.Values.ToArray();

                network.SendAll(new UpdatePartyBehaviorPacket(ref datas));
                updateStore.Clear();
            }
        }
        
    }
}