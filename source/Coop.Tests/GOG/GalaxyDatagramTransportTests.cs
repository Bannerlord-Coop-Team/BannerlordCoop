using Common.Network.Session;
using Coop.GOG;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Coop.Tests.GOG;

public class GalaxyDatagramTransportTests
{
    [Fact]
    public void Prepare_RequiresAuthenticatedLocalGalaxyIdentity()
    {
        var sdk = new FakeGalaxySdk { LocalUserId = 0 };
        using var transport = new GalaxyDatagramTransport(sdk);

        Assert.Throws<System.InvalidOperationException>(transport.Prepare);
    }

    [Fact]
    public void OutgoingMissionConnection_UsesRequestChannelAndReceivesOnlyReplyChannel()
    {
        var sdk = new FakeGalaxySdk();
        using var transport = new GalaxyDatagramTransport(sdk);
        var remote = new PlatformIdentity("gog", "42");
        long connection = transport.Connect(remote, ProviderTunnel.MissionChannel);
        byte[] outbound = Enumerable.Range(0, 1500).Select(index => (byte)index).ToArray();

        Assert.True(transport.Send(connection, outbound, outbound.Length, droppable: false));
        Assert.All(sdk.SentPackets, packet => Assert.Equal((byte)2, packet.Channel));
        Assert.All(sdk.SentPackets, packet => Assert.Equal(GalaxyP2PSendMode.Reliable, packet.SendMode));

        RaiseDatagram(sdk, 42, channel: 3, messageId: 77, new byte[] { 9, 8, 7 });
        var buffer = new byte[ProviderTunnel.MaxDatagramBytes];
        int received = transport.Receive(connection, buffer);
        Assert.Equal(new byte[] { 9, 8, 7 }, buffer.Take(received).ToArray());
    }

    [Fact]
    public void Listener_AcceptsAuthenticatedSenderAndRepliesOnSeparateChannel()
    {
        var sdk = new FakeGalaxySdk();
        using var transport = new GalaxyDatagramTransport(sdk);
        var states = new List<(long Connection, ProviderConnectionState State)>();
        transport.ConnectionStateChanged += (connection, state) =>
        {
            states.Add((connection, state));
            if (state == ProviderConnectionState.Connecting) transport.Accept(connection);
        };
        transport.Listen(ProviderTunnel.MissionChannel);

        RaiseDatagram(sdk, 42, channel: 2, messageId: 1, new byte[] { 1, 2, 3 });

        Assert.Equal(ProviderConnectionState.Connecting, states[0].State);
        Assert.Equal(ProviderConnectionState.Connected, states[1].State);
        long connection = states[0].Connection;
        Assert.True(transport.TryGetRemoteIdentity(connection, out var identity));
        Assert.Equal(new PlatformIdentity("gog", "42"), identity);
        var buffer = new byte[ProviderTunnel.MaxDatagramBytes];
        Assert.Equal(3, transport.Receive(connection, buffer));

        Assert.True(transport.Send(connection, new byte[] { 5 }, 1, droppable: true));
        var reply = Assert.Single(sdk.SentPackets);
        Assert.Equal((byte)3, reply.Channel);
        Assert.Equal(GalaxyP2PSendMode.Unreliable, reply.SendMode);
    }

    [Fact]
    public void SharedSdk_ReplyIsConsumedByOutgoingTransportNotListener()
    {
        var sdk = new FakeGalaxySdk();
        using var host = new GalaxyDatagramTransport(sdk);
        using var client = new GalaxyDatagramTransport(sdk);
        int hostStateChanges = 0;
        host.ConnectionStateChanged += (_, _) => hostStateChanges++;
        host.Listen(ProviderTunnel.MissionChannel);
        long clientConnection = client.Connect(
            new PlatformIdentity("gog", "42"),
            ProviderTunnel.MissionChannel);

        RaiseDatagram(sdk, 42, channel: 3, messageId: 1, new byte[] { 7 });

        var buffer = new byte[ProviderTunnel.MaxDatagramBytes];
        Assert.Equal(1, client.Receive(clientConnection, buffer));
        Assert.Equal(0, hostStateChanges);
    }

    [Fact]
    public void PacketSenderIsPartOfAuthenticatedConnectionMapping()
    {
        var sdk = new FakeGalaxySdk();
        using var transport = new GalaxyDatagramTransport(sdk);
        long connection = transport.Connect(
            new PlatformIdentity("gog", "42"),
            ProviderTunnel.SessionChannel);

        RaiseDatagram(sdk, 43, channel: 1, messageId: 1, new byte[] { 9 });
        var buffer = new byte[ProviderTunnel.MaxDatagramBytes];
        Assert.Equal(0, transport.Receive(connection, buffer));

        RaiseDatagram(sdk, 42, channel: 1, messageId: 2, new byte[] { 8 });
        Assert.Equal(1, transport.Receive(connection, buffer));
        Assert.Equal(8, buffer[0]);
    }

    [Fact]
    public void ReliableCloseFrame_RemovesConnectionAndRaisesClosed()
    {
        var sdk = new FakeGalaxySdk();
        using var transport = new GalaxyDatagramTransport(sdk);
        long closedConnection = 0;
        transport.ConnectionStateChanged += (connection, state) =>
        {
            if (state == ProviderConnectionState.Connecting) transport.Accept(connection);
            if (state == ProviderConnectionState.Closed) closedConnection = connection;
        };
        transport.Listen(ProviderTunnel.SessionChannel);
        RaiseDatagram(sdk, 42, channel: 0, messageId: 1, new byte[] { 1 });
        Assert.True(transport.TryGetRemoteIdentity(1, out _));

        sdk.RaisePacket(42, 0, GalaxyFragmentCodec.EncodeClose(2));

        Assert.Equal(1, closedConnection);
        Assert.False(transport.TryGetRemoteIdentity(1, out _));
    }

    [Fact]
    public void Close_SendsReliableFrameOnConnectionSendChannel()
    {
        var sdk = new FakeGalaxySdk();
        using var transport = new GalaxyDatagramTransport(sdk);
        long connection = transport.Connect(
            new PlatformIdentity("gog", "42"),
            ProviderTunnel.MissionChannel);

        transport.Close(connection);

        var packet = Assert.Single(sdk.SentPackets);
        Assert.Equal((byte)2, packet.Channel);
        Assert.Equal(GalaxyP2PSendMode.Reliable, packet.SendMode);
        Assert.True(GalaxyFragmentCodec.TryDecode(packet.Data, out var fragment));
        Assert.True(fragment.Close);
    }

    private static void RaiseDatagram(
        FakeGalaxySdk sdk,
        ulong sender,
        byte channel,
        uint messageId,
        byte[] data)
    {
        foreach (byte[] packet in GalaxyFragmentCodec.Encode(messageId, data, data.Length))
            sdk.RaisePacket(sender, channel, packet);
    }
}
