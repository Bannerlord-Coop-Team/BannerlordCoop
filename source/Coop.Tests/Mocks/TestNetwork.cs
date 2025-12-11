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

public class TestNetwork : INetwork
{
    private class TestNetworkConfiguration : INetworkConfiguration
    {
        public string Address => "localhost";
        public int Port => 4200;
        public string Token => "TempToken";
        public string P2PToken => "";
        public TimeSpan ConnectionTimeout => TimeSpan.FromSeconds(60);
        public int MaxPacketsInQueue => 3000;
        public TimeSpan AuditTimeout => TimeSpan.FromSeconds(15);
        public TimeSpan ObjectCreationTimeout => TimeSpan.FromSeconds(5);
        public TimeSpan NetworkPollInterval => TimeSpan.FromMilliseconds(50);
        public bool AllowAutoPause => true;
    }

    public INetworkConfiguration Configuration { get; } = new TestNetworkConfiguration();

    public int Priority => throw new NotImplementedException();

    public readonly Dictionary<int, List<IMessage>> SentNetworkMessages = new Dictionary<int, List<IMessage>>();
    public readonly Dictionary<int, List<IPacket>> SentPackets = new Dictionary<int, List<IPacket>>();
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

    public void Clear()
    {
        SentNetworkMessages.Clear();
        SentPackets.Clear();
    }

    public void Dispose()
    {
        Clear();
    }
}
