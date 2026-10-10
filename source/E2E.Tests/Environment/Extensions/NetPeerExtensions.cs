using Common.Util;
using LiteNetLib;
using System;
using System.Net;
using System.Reflection;
using System.Threading;

namespace E2E.Tests.Environment.Extensions;

/// <summary>
/// Extensions for the NetPeer class
/// </summary>
internal static class NetPeerExtensions
{
    private static int endpointSequence;
    private static readonly IPAddress MockAddress = IPAddress.Parse("127.0.0.2");
    private static readonly FieldInfo Id = typeof(NetPeer).GetField(nameof(NetPeer.Id))!;
    private static readonly ConstructorInfo Ctor = typeof(NetPeer).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, new Type[]
    {
        typeof(NetManager),
        typeof(IPEndPoint),
        typeof(int),
    })!;
    public static void SetId(this NetPeer peer, int id)
    {
        Id.SetValue(peer, id);
    }

    public static NetPeer CreatePeer()
    {
        var peer = ObjectHelper.SkipConstructor<NetPeer>();
        // NetPeer inherits endpoint equality, so dictionary keys need distinct initialized endpoints.
        peer.Address = MockAddress;
        peer.Port = 1 + (int)(unchecked((uint)Interlocked.Increment(ref endpointSequence)) % 60000);
        return peer;
    }

    public static NetPeer CreatePeer(int id)
    {
        var newPeer = CreatePeer();
        var manager = new NetManager(null);
        var endPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5555 + id);

        Ctor.Invoke(newPeer, new object[] { manager, endPoint, id });

        return newPeer;
    }
}
