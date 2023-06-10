using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using Coop.Tests.Extensions;
using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using TaleWorlds.Library;

namespace Coop.Tests.Mocks;

public class MockNetwork : INetwork
{
    public INetworkConfiguration Configuration => throw new NotImplementedException();

    public int Priority => throw new NotImplementedException();

    public readonly Dictionary<int, List<IMessage>> SentNetworkMessages = new Dictionary<int, List<IMessage>>();
    public readonly Dictionary<int, List<IPacket>> SentPackets = new Dictionary<int, List<IPacket>>();
    public readonly List<NetPeer> Peers = new List<NetPeer>();

    private static int NewPeerId => Interlocked.Increment(ref _peerId);
    private static int _peerId = 0;

    public NetPeer CreatePeer()
    {
        var newPeer = (NetPeer)FormatterServices.GetUninitializedObject(typeof(NetPeer));
        newPeer.SetId(NewPeerId);

        Peers.Add(newPeer);

        return newPeer;
    }

    public IEnumerable<IMessage> GetPeerMessages(NetPeer peer)
    {
        return SentNetworkMessages[peer.Id];
    }

    public IEnumerable<IPacket> GetPeerPackets(NetPeer peer)
    {
        return SentPackets[peer.Id];
    }

    public void Send(NetPeer netPeer, IPacket packet)
    {
        var packets = SentPackets.ContainsKey(netPeer.Id) ?
                        SentPackets[netPeer.Id] : new List<IPacket>();
        packets.Add(packet);

        SentPackets[netPeer.Id] = packets;
    }

    public void SendAll(IPacket packet)
    {
        foreach (var peer in Peers)
        {
            Send(peer, packet);
        }
    }

    public void SendAllBut(NetPeer excludedPeer, IPacket packet)
    {
        foreach (var peer in Peers.Where(p => p != excludedPeer))
        {
            Send(peer, packet);
        }
    }

    public void Send(NetPeer netPeer, IMessage message)
    {
        var messages = SentNetworkMessages.ContainsKey(netPeer.Id) ?
                        SentNetworkMessages[netPeer.Id] : new List<IMessage>();
        messages.Add(message);

        SentNetworkMessages[netPeer.Id] = messages;
    }

    public void SendAll(IMessage message)
    {
        foreach(var peer in Peers)
        {
            Send(peer, message);
        }
    }

    public void SendAllBut(NetPeer excludedPeer, IMessage message)
    {
        foreach (var peer in Peers.Where(p => p != excludedPeer))
        {
            Send(peer, message);
        }
    }

    public void Start()
    {
    }

    public void Stop()
    {
    }

    public void Update(TimeSpan frameTime)
    {
    }
}
