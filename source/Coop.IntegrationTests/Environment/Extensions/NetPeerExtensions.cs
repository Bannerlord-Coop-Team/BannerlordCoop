using LiteNetLib;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;

namespace Coop.IntegrationTests.Environment.Extensions;

/// <summary>
/// Extensions for the NetPeer class
/// </summary>
internal static class NetPeerExtensions
{
    private static readonly FieldInfo Id = typeof(NetPeer).GetField(nameof(NetPeer.Id))!;

    private static int _portCounter;

    public static void SetId(this NetPeer peer, int id)
    {
        Id.SetValue(peer, id);
    }

    public static NetPeer CreatePeer()
    {
        var peer = (NetPeer)FormatterServices.GetUninitializedObject(typeof(NetPeer));

        // NetPeer derives from IPEndPoint, and its equality/hash come from the endpoint's Address+Port.
        // An uninitialized mock has a null Address and throws inside IPEndPoint.Equals when used as a
        // dictionary key (e.g. MissionInstance's controller<->peer maps). Give each a distinct loopback
        // endpoint so mocks behave like real, distinct connections.
        var endPoint = (IPEndPoint)peer;
        endPoint.Address = IPAddress.Loopback;
        endPoint.Port = 1 + Interlocked.Increment(ref _portCounter) % 60000;

        return peer;
    }

    public static NetPeer CreatePeer(int id)
    {
        var newPeer = CreatePeer();

        newPeer.SetId(id);

        return newPeer;
    }
}
