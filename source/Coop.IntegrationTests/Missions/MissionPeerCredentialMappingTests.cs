using Common.Messaging;
using Common.Network;
using Common.Network.Data;
using Common.Network.Session;
using Common.PacketHandlers;
using Common.Serialization;
using Common.Tests.Utils;
using GameInterface.Services.Entity;
using LiteNetLib;
using Missions.Messages;
using Missions.Services.Network;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Coop.IntegrationTests.Missions;

public class MissionPeerCredentialMappingTests
{
    private const string InstanceId = "battle-instance";
    private static readonly ConstructorInfo PeerConstructor = typeof(NetPeer).GetConstructor(
        BindingFlags.NonPublic | BindingFlags.Instance,
        binder: null,
        new[] { typeof(NetManager), typeof(IPEndPoint), typeof(int) },
        modifiers: null)!;

    [Fact]
    public void ConnectionRequestWithoutCredential_IsRejected()
    {
        using var fixture = new Fixture(startNetwork: true);
        fixture.Announce("direct-host", steamId: 0, Guid.NewGuid());

        fixture.ConnectFromRemote(new ConnectionToken("direct-host", InstanceId), expectConnected: false);

        fixture.MissionContext.Verify(
            context => context.MapPeer(It.IsAny<string>(), It.IsAny<NetPeer>()),
            Times.Never);
        Assert.Empty(fixture.PendingPeers);
    }

    [Fact]
    public void ConnectionRequestWithMismatchedCredential_IsRejected()
    {
        using var fixture = new Fixture(startNetwork: true);
        fixture.Announce("direct-host", steamId: 0, Guid.NewGuid());

        fixture.ConnectFromRemote(
            new ConnectionToken("direct-host", InstanceId, Guid.NewGuid()),
            expectConnected: false);

        fixture.MissionContext.Verify(
            context => context.MapPeer(It.IsAny<string>(), It.IsAny<NetPeer>()),
            Times.Never);
        Assert.Empty(fixture.PendingPeers);
    }

    [Fact]
    public void ConnectionRequestWithMatchingCredential_MapsPeer()
    {
        using var fixture = new Fixture(startNetwork: true);
        var credential = Guid.NewGuid();
        fixture.Announce("direct-host", steamId: 0, credential);

        fixture.ConnectFromRemote(
            new ConnectionToken("direct-host", InstanceId, credential),
            expectConnected: true);

        fixture.MissionContext.Verify(
            context => context.MapPeer("direct-host", It.IsAny<NetPeer>()),
            Times.Once);
    }

    [Fact]
    public void ConnectionRequestWithWrongAuthenticatedSteamIdentity_IsRejected()
    {
        using var fixture = new Fixture(startNetwork: true, authenticatedSteamId: 9001);
        var credential = Guid.NewGuid();
        fixture.Announce("steam-peer", steamId: 9002, credential);

        fixture.ConnectFromRemote(
            new ConnectionToken("steam-peer", InstanceId, credential),
            expectConnected: false);

        fixture.MissionContext.Verify(
            context => context.MapPeer(It.IsAny<string>(), It.IsAny<NetPeer>()),
            Times.Never);
        Assert.Empty(fixture.PendingPeers);
    }

    [Fact]
    public void AuthenticatedSteamReplacement_WaitsForRotatedAnnouncementThenMaps()
    {
        using var fixture = new Fixture(startNetwork: true, authenticatedSteamId: 9001);
        var oldCredential = Guid.NewGuid();
        var replacementCredential = Guid.NewGuid();
        fixture.Announce("steam-peer", steamId: 9001, oldCredential);
        var oldPeer = fixture.TrackPending("steam-peer", oldCredential, actualSteamId: 9001);
        fixture.Client.OnPeerConnected(oldPeer);

        fixture.ConnectFromRemote(
            new ConnectionToken("steam-peer", InstanceId, replacementCredential),
            expectConnected: true,
            afterConnected: () =>
            {
                fixture.MissionContext.Verify(
                    context => context.MapPeer("steam-peer", It.IsAny<NetPeer>()),
                    Times.Once);
                fixture.Announce("steam-peer", steamId: 9001, replacementCredential);
            },
            expectedMapInvocations: 2);

        Assert.DoesNotContain(oldPeer, fixture.MappedPeers.Keys);
        fixture.MissionContext.Verify(context => context.RemovePeer(oldPeer), Times.Once);
        fixture.MissionContext.Verify(
            context => context.MapPeer("steam-peer", It.IsAny<NetPeer>()),
            Times.Exactly(2));
    }

    [Fact]
    public void AuthenticatedSteamReplacementAfterOldRouteDisconnect_WaitsForRotatedAnnouncement()
    {
        using var fixture = new Fixture(startNetwork: true, authenticatedSteamId: 9001);
        var oldCredential = Guid.NewGuid();
        var replacementCredential = Guid.NewGuid();
        fixture.Announce("steam-peer", steamId: 9001, oldCredential);
        var oldPeer = fixture.TrackPending("steam-peer", oldCredential, actualSteamId: 9001);
        fixture.Client.OnPeerConnected(oldPeer);
        fixture.Client.OnPeerDisconnected(oldPeer, default);

        fixture.ConnectFromRemote(
            new ConnectionToken("steam-peer", InstanceId, replacementCredential),
            expectConnected: true,
            afterConnected: () =>
            {
                fixture.MissionContext.Verify(
                    context => context.MapPeer("steam-peer", It.IsAny<NetPeer>()),
                    Times.Once);
                fixture.Announce("steam-peer", steamId: 9001, replacementCredential);
            },
            expectedMapInvocations: 2);

        fixture.MissionContext.Verify(context => context.RemovePeer(oldPeer), Times.Once);
        fixture.MissionContext.Verify(
            context => context.MapPeer("steam-peer", It.IsAny<NetPeer>()),
            Times.Exactly(2));
    }

    [Fact]
    public void ZeroSteamIdAnnouncementDisconnect_ClosesAuthenticatedSteamTunnel()
    {
        using var fixture = new Fixture();
        var credential = Guid.NewGuid();
        fixture.Announce("direct-host", steamId: 0, credential);
        var peer = fixture.TrackPending("direct-host", credential, actualSteamId: 9001);
        fixture.Client.OnPeerConnected(peer);

        fixture.Client.OnPeerDisconnected(peer, default);

        fixture.SteamBridge.Verify(bridge => bridge.Disconnect(9001), Times.Once);
        fixture.MissionContext.Verify(context => context.RemovePeer(peer), Times.Once);
    }

    [Fact]
    public void ZeroSteamIdAnnouncementSteamDisconnect_RemovesAuthenticatedRoute()
    {
        using var fixture = new Fixture();
        var credential = Guid.NewGuid();
        fixture.Announce("direct-host", steamId: 0, credential);
        var peer = fixture.TrackPending("direct-host", credential, actualSteamId: 9001);
        fixture.Client.OnPeerConnected(peer);

        fixture.SteamBridge.Raise(bridge => bridge.PeerDisconnected += null, 9001UL);

        Assert.DoesNotContain(peer, fixture.MappedPeers.Keys);
        fixture.MissionContext.Verify(context => context.RemovePeer(peer), Times.Once);
    }

    [Fact]
    public void ZeroSteamIdAnnouncementDeparture_ClosesAuthenticatedSteamTunnel()
    {
        using var fixture = new Fixture();
        var credential = Guid.NewGuid();
        fixture.Announce("direct-host", steamId: 0, credential);
        var peer = fixture.TrackPending("direct-host", credential, actualSteamId: 9001);
        fixture.Client.OnPeerConnected(peer);

        fixture.Depart("direct-host");

        fixture.SteamBridge.Verify(bridge => bridge.Disconnect(9001), Times.Once);
        Assert.DoesNotContain(peer, fixture.MappedPeers.Keys);
        fixture.MissionContext.Verify(context => context.RemovePeer(peer), Times.Once);
    }

    [Fact]
    public void OldRouteDisconnectDuringAuthenticatedReplacement_KeepsSharedSteamTunnel()
    {
        using var fixture = new Fixture(startNetwork: true, authenticatedSteamId: 9001);
        var oldCredential = Guid.NewGuid();
        var replacementCredential = Guid.NewGuid();
        fixture.Announce("direct-host", steamId: 0, oldCredential);
        var oldPeer = fixture.TrackPending("direct-host", oldCredential, actualSteamId: 9001);
        fixture.Client.OnPeerConnected(oldPeer);

        fixture.ConnectFromRemote(
            new ConnectionToken("direct-host", InstanceId, replacementCredential),
            expectConnected: true,
            afterConnected: () =>
            {
                fixture.Client.OnPeerDisconnected(oldPeer, default);
                fixture.SteamBridge.Verify(bridge => bridge.Disconnect(9001), Times.Never);
                fixture.Announce("direct-host", steamId: 0, replacementCredential);
            },
            expectedMapInvocations: 2);
    }

    [Fact]
    public void AuthenticatedSteamReplacementForZeroSteamAnnouncement_WaitsThenMaps()
    {
        using var fixture = new Fixture(startNetwork: true, authenticatedSteamId: 9001);
        var oldCredential = Guid.NewGuid();
        var replacementCredential = Guid.NewGuid();
        fixture.Announce("direct-host", steamId: 0, oldCredential);
        var oldPeer = fixture.TrackPending("direct-host", oldCredential, actualSteamId: 0);
        fixture.Client.OnPeerConnected(oldPeer);

        fixture.ConnectFromRemote(
            new ConnectionToken("direct-host", InstanceId, replacementCredential),
            expectConnected: true,
            afterConnected: () =>
            {
                fixture.MissionContext.Verify(
                    context => context.MapPeer("direct-host", It.IsAny<NetPeer>()),
                    Times.Once);
                fixture.Announce("direct-host", steamId: 0, replacementCredential);
            },
            expectedMapInvocations: 2);

        Assert.DoesNotContain(oldPeer, fixture.MappedPeers.Keys);
        fixture.MissionContext.Verify(context => context.RemovePeer(oldPeer), Times.Once);
        fixture.MissionContext.Verify(
            context => context.MapPeer("direct-host", It.IsAny<NetPeer>()),
            Times.Exactly(2));
    }

    [Fact]
    public void ZeroSteamIdAnnouncementBeforeConnection_MapsMatchingCredential()
    {
        using var fixture = new Fixture();
        var credential = Guid.NewGuid();
        fixture.Announce("direct-host", steamId: 0, credential);
        var peer = fixture.TrackPending("direct-host", credential, actualSteamId: 9001);

        fixture.Client.OnPeerConnected(peer);

        fixture.MissionContext.Verify(
            context => context.MapPeer("direct-host", peer),
            Times.Once);
    }

    [Fact]
    public void ConnectionBeforeZeroSteamIdAnnouncement_WaitsThenMapsMatchingCredential()
    {
        using var fixture = new Fixture();
        var credential = Guid.NewGuid();
        var peer = fixture.TrackPending("direct-host", credential, actualSteamId: 9001);

        fixture.Client.OnPeerConnected(peer);
        fixture.MissionContext.Verify(
            context => context.MapPeer(It.IsAny<string>(), It.IsAny<NetPeer>()),
            Times.Never);

        fixture.Announce("direct-host", steamId: 0, credential);

        fixture.MissionContext.Verify(
            context => context.MapPeer("direct-host", peer),
            Times.Once);
    }

    [Theory]
    [InlineData(DeliveryMethod.ReliableOrdered)]
    [InlineData(DeliveryMethod.ReliableUnordered)]
    [InlineData(DeliveryMethod.ReliableSequenced)]
    public void ReliablePayloadBeforeAnnouncement_IsDeliveredOnlyAfterMapping(DeliveryMethod method)
    {
        using var fixture = new Fixture(startNetwork: true);
        var credential = Guid.NewGuid();

        fixture.ConnectFromRemote(
            new ConnectionToken("direct-host", InstanceId, credential),
            expectConnected: true,
            beforeMapping: remotePeer =>
            {
                remotePeer.Send(new byte[] { 1 }, method);
                fixture.WaitForBufferedPayloads(1);
                remotePeer.Send(new byte[] { 2 }, method);
                fixture.WaitForBufferedPayloads(2);
                fixture.Serializer.Verify(value => value.Deserialize(It.IsAny<byte[]>()), Times.Never);
                Assert.Empty(fixture.ReceivedPayloads);

                fixture.Announce("direct-host", steamId: 0, credential);
                remotePeer.Send(new byte[] { 3 }, method);
            },
            afterMapping: () =>
            {
                Assert.True(SpinWait.SpinUntil(
                    () => fixture.ReceivedPayloads.Count == 3,
                    TimeSpan.FromSeconds(5)), "Buffered reliable mission payloads were not delivered.");
                Assert.Equal(new[] { 1, 2, 3 }, fixture.ReceivedPayloads.ToArray());
                fixture.WaitForBufferedPayloads(0);
            });
    }

    [Theory]
    [InlineData("credential-mismatch")]
    [InlineData("steam-mismatch")]
    [InlineData("departure")]
    [InlineData("disconnect")]
    [InlineData("local-rotation")]
    [InlineData("instance-change")]
    [InlineData("mission-end")]
    public void PendingReliablePayloads_AreDiscardedWhenRouteIsInvalidated(string reason)
    {
        using var fixture = new Fixture(startNetwork: true);
        var credential = Guid.NewGuid();
        fixture.SetLocalCredential(Guid.NewGuid());

        fixture.ConnectFromRemote(
            new ConnectionToken("direct-host", InstanceId, credential),
            expectConnected: true,
            expectedMapInvocations: 0,
            beforeMapping: remotePeer =>
            {
                remotePeer.Send(new byte[] { 1 }, DeliveryMethod.ReliableOrdered);
                fixture.WaitForBufferedPayloads(1);
                switch (reason)
                {
                    case "credential-mismatch":
                        fixture.Announce("direct-host", steamId: 0, Guid.NewGuid());
                        break;
                    case "steam-mismatch":
                        fixture.Announce("direct-host", steamId: 9001, credential);
                        break;
                    case "departure":
                        fixture.Depart("direct-host");
                        break;
                    case "disconnect":
                        remotePeer.Disconnect();
                        break;
                    case "local-rotation":
                        fixture.IssueLocalCredential(Guid.NewGuid());
                        break;
                    case "instance-change":
                        fixture.Client.ConnectToInstance("another-instance");
                        break;
                    case "mission-end":
                        fixture.Client.DisconnectPeers();
                        break;
                }
                fixture.WaitForBufferedPayloads(0);
                fixture.Serializer.Verify(value => value.Deserialize(It.IsAny<byte[]>()), Times.Never);
                Assert.Empty(fixture.ReceivedPayloads);
            });
    }

    [Fact]
    public void PendingReliablePayloadLimit_DisconnectsInsteadOfDroppingAcknowledgedState()
    {
        using var fixture = new Fixture(startNetwork: true);
        fixture.ConnectFromRemote(
            new ConnectionToken("direct-host", InstanceId, Guid.NewGuid()),
            expectConnected: true,
            expectedMapInvocations: 0,
            beforeMapping: remotePeer =>
            {
                for (int i = 0; i < 4096; i++)
                    remotePeer.Send(new byte[] { 1 }, DeliveryMethod.ReliableOrdered);
                fixture.WaitForBufferedPayloads(4096);
                remotePeer.Send(new byte[] { 2 }, DeliveryMethod.ReliableOrdered);
                fixture.WaitForBufferedPayloads(0);
                Assert.True(SpinWait.SpinUntil(
                    () => fixture.Client.ConnectedPeersCount == 0,
                    TimeSpan.FromSeconds(5)), "The overflowing unauthenticated route remained connected.");
                fixture.Serializer.Verify(value => value.Deserialize(It.IsAny<byte[]>()), Times.Never);
            });
    }

    [Theory]
    [InlineData("credential-rotation")]
    [InlineData("departure")]
    [InlineData("instance-change")]
    [InlineData("mission-end")]
    public void MappedBufferedPayload_InvalidatedDuringDeserializationIsNotPublished(string reason)
    {
        using var fixture = new Fixture(startNetwork: true);
        using var deserializing = new ManualResetEventSlim();
        using var allowDispatch = new ManualResetEventSlim();
        using var invalidating = new ManualResetEventSlim();
        using var deserialized = new ManualResetEventSlim();
        var credential = Guid.NewGuid();
        fixture.Serializer.Setup(value => value.Deserialize(It.IsAny<byte[]>()))
            .Returns<byte[]>(data =>
            {
                deserializing.Set();
                if (!allowDispatch.Wait(TimeSpan.FromSeconds(5)))
                    throw new TimeoutException("The test did not release buffered dispatch.");
                deserialized.Set();
                return new ReceivedMessage(data[0]);
            });

        fixture.ConnectFromRemote(
            new ConnectionToken("direct-host", InstanceId, credential),
            expectConnected: true,
            beforeMapping: remotePeer =>
            {
                remotePeer.Send(new byte[] { 1 }, DeliveryMethod.ReliableOrdered);
                fixture.WaitForBufferedPayloads(1);
                fixture.Announce("direct-host", steamId: 0, credential);
                Task? invalidation = null;
                try
                {
                    Assert.True(deserializing.Wait(TimeSpan.FromSeconds(5)));
                    invalidation = Task.Run(() =>
                    {
                        invalidating.Set();
                        switch (reason)
                        {
                            case "credential-rotation":
                                fixture.Announce("direct-host", steamId: 0, Guid.NewGuid());
                                break;
                            case "departure":
                                fixture.Depart("direct-host");
                                break;
                            case "instance-change":
                                fixture.Client.ConnectToInstance("another-instance");
                                break;
                            case "mission-end":
                                fixture.Client.DisconnectPeers();
                                break;
                        }
                    });
                    Assert.True(invalidating.Wait(TimeSpan.FromSeconds(5)));
                    Assert.True(invalidation.Wait(TimeSpan.FromSeconds(5)),
                        "Route invalidation blocked behind a receive handler.");
                }
                finally
                {
                    allowDispatch.Set();
                    if (invalidation != null)
                        Assert.True(invalidation.Wait(TimeSpan.FromSeconds(5)));
                }
                Assert.True(deserialized.Wait(TimeSpan.FromSeconds(5)));
                fixture.WaitForReceiveTick();
                Assert.Empty(fixture.ReceivedPayloads);
                fixture.WaitForBufferedPayloads(0);
            });
    }

    [Fact]
    public void LocalCredentialRotation_ClearsOldRouteBeforeAcceptingReplacementConnection()
    {
        using var fixture = new Fixture(startNetwork: true);
        var remoteCredential = Guid.NewGuid();
        fixture.Announce("direct-host", steamId: 0, remoteCredential);
        var oldPeer = fixture.TrackPending("direct-host", remoteCredential, actualSteamId: 0);
        fixture.Client.OnPeerConnected(oldPeer);
        fixture.IssueLocalCredential(Guid.NewGuid());

        fixture.IssueLocalCredential(Guid.NewGuid());

        Assert.DoesNotContain(oldPeer, fixture.MappedPeers.Keys);
        fixture.MissionContext.Verify(context => context.RemovePeer(oldPeer), Times.Once);
        fixture.ConnectFromRemote(
            new ConnectionToken("direct-host", InstanceId, remoteCredential),
            expectConnected: true,
            expectedMapInvocations: 2);
        fixture.MissionContext.Verify(
            context => context.MapPeer("direct-host", It.IsAny<NetPeer>()),
            Times.Exactly(2));
    }

    [Fact]
    public void NatIntroductionWithRotatedCredential_WaitsForAnnouncementThenReplacesOldRoute()
    {
        using var fixture = new Fixture(startNetwork: true);
        var oldCredential = Guid.NewGuid();
        var replacementCredential = Guid.NewGuid();
        fixture.Announce("direct-host", steamId: 0, oldCredential);
        var oldPeer = fixture.TrackPending("direct-host", oldCredential, actualSteamId: 0);
        fixture.Client.OnPeerConnected(oldPeer);
        fixture.SetLocalCredential(Guid.NewGuid());

        fixture.Client.OnNatIntroductionSuccess(
            new IPEndPoint(IPAddress.Loopback, 65529),
            NatAddressType.Internal,
            new ConnectionToken("direct-host", InstanceId, oldCredential));
        Assert.Empty(fixture.PendingPeers);

        fixture.Client.OnNatIntroductionSuccess(
            new IPEndPoint(IPAddress.Loopback, 65530),
            NatAddressType.Internal,
            new ConnectionToken("direct-host", InstanceId, replacementCredential));
        Assert.Empty(fixture.PendingPeers);

        fixture.Announce("direct-host", steamId: 0, replacementCredential);

        var replacementPeer = Assert.Single(
            fixture.PendingPeers,
            pair => pair.Value == "direct-host").Key;
        Assert.Equal(replacementCredential, fixture.GetPeerCredential(replacementPeer));
        fixture.Client.OnPeerConnected(replacementPeer);

        Assert.DoesNotContain(oldPeer, fixture.MappedPeers.Keys);
        Assert.Equal("direct-host", fixture.MappedPeers[replacementPeer]);
        fixture.MissionContext.Verify(context => context.RemovePeer(oldPeer), Times.Once);
        fixture.MissionContext.Verify(
            context => context.MapPeer("direct-host", It.IsAny<NetPeer>()),
            Times.Exactly(2));
    }

    [Fact]
    public void NatIntroductionAfterOldRouteDisconnect_WaitsForRotatedAnnouncement()
    {
        using var fixture = new Fixture(startNetwork: true);
        var oldCredential = Guid.NewGuid();
        var replacementCredential = Guid.NewGuid();
        fixture.Announce("direct-host", steamId: 0, oldCredential);
        var oldPeer = fixture.TrackPending("direct-host", oldCredential, actualSteamId: 0);
        fixture.Client.OnPeerConnected(oldPeer);
        fixture.SetLocalCredential(Guid.NewGuid());
        fixture.Client.OnPeerDisconnected(oldPeer, default);

        fixture.Client.OnNatIntroductionSuccess(
            new IPEndPoint(IPAddress.Loopback, 65530),
            NatAddressType.Internal,
            new ConnectionToken("direct-host", InstanceId, replacementCredential));
        Assert.Empty(fixture.PendingPeers);

        fixture.Announce("direct-host", steamId: 0, replacementCredential);

        var replacementPeer = Assert.Single(
            fixture.PendingPeers,
            pair => pair.Value == "direct-host").Key;
        fixture.Client.OnPeerConnected(replacementPeer);

        Assert.Equal("direct-host", fixture.MappedPeers[replacementPeer]);
        fixture.MissionContext.Verify(
            context => context.MapPeer("direct-host", It.IsAny<NetPeer>()),
            Times.Exactly(2));
    }

    [Fact]
    public void DelayedOldNatIntroductionAfterRotatedAnnouncement_DoesNotBlockCurrentRoute()
    {
        using var fixture = new Fixture(startNetwork: true);
        var oldCredential = Guid.NewGuid();
        var currentCredential = Guid.NewGuid();
        fixture.Announce("direct-host", steamId: 0, oldCredential);
        var oldPeer = fixture.TrackPending("direct-host", oldCredential, actualSteamId: 0);
        fixture.Client.OnPeerConnected(oldPeer);
        fixture.SetLocalCredential(Guid.NewGuid());

        fixture.Announce("direct-host", steamId: 0, currentCredential);
        fixture.Client.OnNatIntroductionSuccess(
            new IPEndPoint(IPAddress.Loopback, 65529),
            NatAddressType.Internal,
            new ConnectionToken("direct-host", InstanceId, oldCredential));

        Assert.Empty(fixture.PendingPeers);

        fixture.Client.OnNatIntroductionSuccess(
            new IPEndPoint(IPAddress.Loopback, 65530),
            NatAddressType.Internal,
            new ConnectionToken("direct-host", InstanceId, currentCredential));

        var currentPeer = Assert.Single(
            fixture.PendingPeers,
            pair => pair.Value == "direct-host").Key;
        Assert.Equal(currentCredential, fixture.GetPeerCredential(currentPeer));
        fixture.Client.OnPeerConnected(currentPeer);

        Assert.DoesNotContain(oldPeer, fixture.MappedPeers.Keys);
        Assert.Equal("direct-host", fixture.MappedPeers[currentPeer]);
        fixture.MissionContext.Verify(context => context.RemovePeer(oldPeer), Times.Once);
        fixture.MissionContext.Verify(
            context => context.MapPeer("direct-host", It.IsAny<NetPeer>()),
            Times.Exactly(2));
    }

    [Fact]
    public void DeferredNatIntroductionForSteamPeer_UsesAuthenticatedSteamRouteAfterAnnouncement()
    {
        using var fixture = new Fixture(startNetwork: true);
        var oldCredential = Guid.NewGuid();
        var currentCredential = Guid.NewGuid();
        var steamEndPoint = new IPEndPoint(IPAddress.Loopback, 65530);
        fixture.SteamBridge
            .Setup(bridge => bridge.TryConnect(9001, out steamEndPoint))
            .Returns(true);
        fixture.Announce("steam-peer", steamId: 9001, oldCredential);
        var oldPeer = fixture.TrackPending("steam-peer", oldCredential, actualSteamId: 9001);
        fixture.Client.OnPeerConnected(oldPeer);
        fixture.SetLocalCredential(Guid.NewGuid());

        fixture.Client.OnNatIntroductionSuccess(
            new IPEndPoint(IPAddress.Loopback, 65529),
            NatAddressType.Internal,
            new ConnectionToken("steam-peer", InstanceId, currentCredential));
        fixture.Announce("steam-peer", steamId: 9001, currentCredential);

        var currentPeer = Assert.Single(
            fixture.PendingPeers,
            pair => pair.Value == "steam-peer").Key;
        Assert.Equal(currentCredential, fixture.GetPeerCredential(currentPeer));
        fixture.Client.OnPeerConnected(currentPeer);

        Assert.Equal("steam-peer", fixture.MappedPeers[currentPeer]);
        fixture.SteamBridge.Verify(
            bridge => bridge.TryConnect(9001, out steamEndPoint),
            Times.Once);
        fixture.MissionContext.Verify(context => context.RemovePeer(oldPeer), Times.Once);
        fixture.MissionContext.Verify(
            context => context.MapPeer("steam-peer", It.IsAny<NetPeer>()),
            Times.Exactly(2));
    }

    [Fact]
    public void DepartureDuringCredentialRotation_RemovesEveryTrackedRoute()
    {
        using var fixture = new Fixture();
        var oldCredential = Guid.NewGuid();
        fixture.Announce("direct-host", steamId: 0, oldCredential);
        var oldPeer = fixture.TrackPending("direct-host", oldCredential, actualSteamId: 0);
        fixture.Client.OnPeerConnected(oldPeer);
        var replacementPeer = fixture.TrackPending(
            "direct-host",
            Guid.NewGuid(),
            actualSteamId: 0);

        fixture.Depart("direct-host");

        Assert.DoesNotContain(oldPeer, fixture.MappedPeers.Keys);
        Assert.DoesNotContain(replacementPeer, fixture.PendingPeers.Keys);
        fixture.MissionContext.Verify(context => context.RemovePeer(oldPeer), Times.Once);
    }

    [Fact]
    public void MultipleZeroSteamIdControllers_MapOnlyTheirDistinctCredentials()
    {
        using var fixture = new Fixture();
        var firstCredential = Guid.NewGuid();
        var secondCredential = Guid.NewGuid();
        var firstPeer = fixture.TrackPending("first", firstCredential, actualSteamId: 9001);
        var secondPeer = fixture.TrackPending("second", secondCredential, actualSteamId: 9002);
        fixture.Client.OnPeerConnected(firstPeer);
        fixture.Client.OnPeerConnected(secondPeer);

        fixture.Announce("second", steamId: 0, secondCredential);
        fixture.Announce("first", steamId: 0, firstCredential);

        fixture.MissionContext.Verify(context => context.MapPeer("first", firstPeer), Times.Once);
        fixture.MissionContext.Verify(context => context.MapPeer("second", secondPeer), Times.Once);
        fixture.MissionContext.Verify(
            context => context.MapPeer("first", secondPeer),
            Times.Never);
        fixture.MissionContext.Verify(
            context => context.MapPeer("second", firstPeer),
            Times.Never);
    }

    [Fact]
    public void CredentialMismatch_RemovesPendingPeerWithoutMapping()
    {
        using var fixture = new Fixture();
        var peer = fixture.TrackPending("direct-host", Guid.NewGuid(), actualSteamId: 9001);
        fixture.Client.OnPeerConnected(peer);

        fixture.Announce("direct-host", steamId: 0, Guid.NewGuid());

        fixture.MissionContext.Verify(
            context => context.MapPeer(It.IsAny<string>(), It.IsAny<NetPeer>()),
            Times.Never);
        Assert.False(fixture.PendingPeers.ContainsKey(peer));
    }

    [Fact]
    public void NonzeroSteamIdentityMismatch_DoesNotMapMatchingCredential()
    {
        using var fixture = new Fixture();
        var credential = Guid.NewGuid();
        var peer = fixture.TrackPending("steam-peer", credential, actualSteamId: 9001);
        fixture.Client.OnPeerConnected(peer);

        fixture.Announce("steam-peer", steamId: 9002, credential);

        fixture.MissionContext.Verify(
            context => context.MapPeer(It.IsAny<string>(), It.IsAny<NetPeer>()),
            Times.Never);
        Assert.False(fixture.PendingPeers.ContainsKey(peer));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly TestMessageBroker messageBroker = new();
        private readonly NetManager netManager;
        private int nextPeerId;

        public Mock<IMissionContext> MissionContext { get; } = new();
        public Mock<ISteamMissionBridge> SteamBridge { get; } = new();
        public Mock<ICommonSerializer> Serializer { get; } = new();
        public ConcurrentQueue<int> ReceivedPayloads { get; } = new();
        public LiteNetP2PClient Client { get; }
        public Dictionary<NetPeer, string> PendingPeers { get; }
        public Dictionary<NetPeer, string> MappedPeers { get; }

        public Fixture(bool startNetwork = false, ulong authenticatedSteamId = 0)
        {
            var config = new Mock<INetworkConfig>();
            config.SetupGet(value => value.DisconnectTimeout).Returns(TimeSpan.FromSeconds(5));
            config.SetupGet(value => value.PingInterval).Returns(TimeSpan.FromSeconds(1));
            config.SetupGet(value => value.ReconnectDelay).Returns(TimeSpan.FromMilliseconds(500));
            config.SetupGet(value => value.IsTunneled).Returns(true);
            var controllerIdProvider = new Mock<IControllerIdProvider>();
            controllerIdProvider.SetupGet(provider => provider.ControllerId).Returns("local");
            if (authenticatedSteamId != 0)
            {
                ulong resolvedSteamId = authenticatedSteamId;
                SteamBridge
                    .Setup(bridge => bridge.TryGetRemoteSteamId(
                        It.IsAny<IPEndPoint>(),
                        out resolvedSteamId))
                    .Returns(true);
            }

            Serializer.Setup(value => value.Deserialize(It.IsAny<byte[]>()))
                .Returns<byte[]>(data => new ReceivedMessage(data[0]));
            var messageHandler = new Mock<IMessagePacketHandler>();
            messageHandler.Setup(value => value.PublishEvent(It.IsAny<NetPeer>(), It.IsAny<IMessage>()))
                .Callback<NetPeer, IMessage>((peer, message) =>
                {
                    ReceivedPayloads.Enqueue(((ReceivedMessage)message).Value);
                });

            Client = new LiteNetP2PClient(
                config.Object,
                new Mock<IRelayNetwork>().Object,
                MissionContext.Object,
                Serializer.Object,
                messageBroker,
                new Mock<IPacketManager>().Object,
                messageHandler.Object,
                controllerIdProvider.Object,
                SteamBridge.Object,
                new Mock<IMovementPacketCompressor>().Object,
                new ReliableMessageBatcher<string>(Serializer.Object));
            Client.ConnectToInstance(InstanceId);
            if (startNetwork) Client.Start();

            netManager = (NetManager)typeof(LiteNetP2PClient)
                .GetField("netManager", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(Client)!;
            PendingPeers = GetDictionary<NetPeer, string>("pendingPeerControllers");
            MappedPeers = GetDictionary<NetPeer, string>("mappedPeerControllers");
        }

        public NetPeer TrackPending(string controllerId, Guid credential, ulong actualSteamId)
        {
            var peer = (NetPeer)PeerConstructor.Invoke(new object[]
            {
                netManager,
                new IPEndPoint(IPAddress.Loopback, 55000 + ++nextPeerId),
                nextPeerId,
            });
            PendingPeers[peer] = controllerId;
            GetDictionary<NetPeer, Guid>("peerCredentials")[peer] = credential;
            GetDictionary<NetPeer, ulong>("peerSteamIds")[peer] = actualSteamId;
            return peer;
        }

        public void Announce(string controllerId, ulong steamId, Guid credential)
        {
            messageBroker.Publish(
                this,
                new NetworkMissionPeerEntered(controllerId, InstanceId, steamId, credential));
        }

        public void Depart(string controllerId)
        {
            messageBroker.Publish(this, new MissionPeerLeft(controllerId, InstanceId));
        }

        public void IssueLocalCredential(Guid credential)
        {
            messageBroker.Publish(
                this,
                new NetworkMissionCredentialIssued(InstanceId, credential));
        }

        public Guid GetPeerCredential(NetPeer peer) =>
            GetDictionary<NetPeer, Guid>("peerCredentials")[peer];

        public void SetLocalCredential(Guid credential)
        {
            typeof(LiteNetP2PClient)
                .GetField("localPeerCredential", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(Client, credential);
        }

        public void ConnectFromRemote(
            ConnectionToken token,
            bool expectConnected,
            Action? afterConnected = null,
            int expectedMapInvocations = 1,
            Action<NetPeer>? beforeMapping = null,
            Action? afterMapping = null)
        {
            var listener = new EventBasedNetListener();
            bool connected = false;
            bool disconnected = false;
            listener.PeerConnectedEvent += _ => connected = true;
            listener.PeerDisconnectedEvent += (_, _) => disconnected = true;
            var remote = new NetManager(listener);
            try
            {
                Assert.True(remote.Start());
                var remotePeer = remote.Connect(
                    new IPEndPoint(IPAddress.Loopback, netManager.LocalPort),
                    (string)token);

                var timeout = Stopwatch.StartNew();
                while (timeout.Elapsed < TimeSpan.FromSeconds(5) &&
                    !(expectConnected ? connected : disconnected))
                {
                    remote.PollEvents();
                    Thread.Sleep(5);
                }

                if (expectConnected)
                {
                    Assert.True(connected, "The matching connection request was not accepted.");
                    beforeMapping?.Invoke(remotePeer);
                    afterConnected?.Invoke();
                    Assert.True(
                        SpinWait.SpinUntil(
                            () => MissionContext.Invocations.Count(invocation =>
                                invocation.Method.Name == nameof(IMissionContext.MapPeer) &&
                                Equals(invocation.Arguments[0], token.ControllerId)) >=
                                expectedMapInvocations,
                            TimeSpan.FromSeconds(5)),
                        "The accepted connection was not mapped to its controller.");
                    afterMapping?.Invoke();
                }
                else
                    Assert.True(disconnected, "The invalid connection request was not rejected.");
            }
            finally
            {
                remote.Stop();
            }
        }

        public void Dispose() => Client.Dispose();

        public void WaitForBufferedPayloads(int expected)
        {
            var gate = typeof(LiteNetP2PClient)
                .GetField("peerGate", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(Client)!;
            var count = typeof(LiteNetP2PClient)
                .GetField("pendingReliablePayloadCount", BindingFlags.NonPublic | BindingFlags.Instance)!;
            Assert.True(SpinWait.SpinUntil(() =>
            {
                lock (gate) return (int)count.GetValue(Client)! == expected;
            }, TimeSpan.FromSeconds(5)), $"Expected {expected} buffered reliable payloads.");
        }

        public void WaitForReceiveTick()
        {
            // Stop only the poller; another route cleanup here would mask a missing generation check.
            var poller = (Common.Util.Poller)typeof(LiteNetP2PClient)
                .GetField("poller", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(Client)!;
            Assert.True(poller.StopAndWait(TimeSpan.FromSeconds(5)),
                "The receive poller did not finish its tick.");
        }

        private Dictionary<TKey, TValue> GetDictionary<TKey, TValue>(string fieldName)
            where TKey : notnull
        {
            return (Dictionary<TKey, TValue>)typeof(LiteNetP2PClient)
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(Client)!;
        }
    }

    private sealed record ReceivedMessage(int Value) : IMessage;
}
