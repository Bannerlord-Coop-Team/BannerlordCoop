using LiteNetLib;
using System.Reflection;
using System.Runtime.Serialization;

namespace E2E.Tests.Environment.Extensions;

/// <summary>
/// Extensions for the NetPeer class
/// </summary>
internal static class NetPeerExtensions
{
    private static readonly FieldInfo Id = typeof(NetPeer).GetField(nameof(NetPeer.Id))!;
    public static void SetId(this NetPeer peer, int id)
    {
        Id.SetValue(peer, id);
    }

    public static NetPeer CreatePeer()
    {
        return (NetPeer)FormatterServices.GetUninitializedObject(typeof(NetPeer));
    }

    public static NetPeer CreatePeer(int id)
    {
        var newPeer = CreatePeer();

        newPeer.SetId(id);

        return newPeer;
    }
}
