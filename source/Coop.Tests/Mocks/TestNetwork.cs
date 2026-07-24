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

namespace Coop.Tests.Mocks;

public class TestNetwork : INetwork
{
    public INetworkConfig Config => throw new NotImplementedException();

    public int Priority => throw new NotImplementedException();

    public readonly Dictionary<int, List<IMessage>> SentNetworkMessages = new Dictionary<int, List<IMessage>>();
    public readonly Dictionary<int, List<IPacket>> SentPackets = new Dictionary<int, List<IPacket>>();
    public readonly Dictionary<int, List<object>> SentPayloads = new Dictionary<int, List<object>>();
    public readonly List<NetPeer> Peers = new List<NetPeer>();

    private static int NewPeerId => Interlocked.Increment(ref _peerId);
    private static int _peerId = 0;

    public NetPeer CreatePeer()
    {
        var newPeer = (NetPeer)FormatterServices.GetUninitializedObject(typeof(NetPeer));
        newPeer.Setup(NewPeerId);

        Peers.Add(newPeer);

        return newPeer;
    }

    public IEnumerable<IMessage> GetPeerMessages(NetPeer peer) => SentNetworkMessages[peer.Id];

    public IEnumerable<T> GetPeerMessagesFromType<T>(NetPeer peer) where T : IMessage
    {
        return SentNetworkMessages[peer.Id].Where(msg => msg is T).Cast<T>();
    }

    public IEnumerable<IPacket> GetPeerPackets(NetPeer peer) => SentPackets[peer.Id];

    public IEnumerable<T> GetPeerPacketsFromType<T>(NetPeer peer) where T : IPacket 
    {
        return SentPackets[peer.Id].Where(pkt => pkt is T).Cast<T>();
    }

    public IEnumerable<object> GetPeerPayloads(NetPeer peer) => SentPayloads[peer.Id];

    public void Send(NetPeer netPeer, IPacket packet)
    {
        var packets = SentPackets.ContainsKey(netPeer.Id) ?
                        SentPackets[netPeer.Id] : new List<IPacket>();
        packets.Add(packet);

        SentPackets[netPeer.Id] = packets;
        RecordPayload(netPeer, packet);
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
        RecordPayload(netPeer, message);
    }

    public void SendImmediate(NetPeer netPeer, IPacket packet) => Send(netPeer, packet);

    public void SendImmediate(NetPeer netPeer, IMessage message) => Send(netPeer, message);

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

    public void Clear()
    {
        SentNetworkMessages.Clear();
        SentPackets.Clear();
        SentPayloads.Clear();
    }

    private void RecordPayload(NetPeer netPeer, object payload)
    {
        var payloads = SentPayloads.ContainsKey(netPeer.Id) ?
                       SentPayloads[netPeer.Id] : new List<object>();
        payloads.Add(payload);

        SentPayloads[netPeer.Id] = payloads;
    }

    public void Dispose()
    {
        Clear();
    }
}
