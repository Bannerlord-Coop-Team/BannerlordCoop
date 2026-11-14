using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Framework.Finalization;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Framework.Registries;
using GameInterface.Services.ObjectManager;
using ProtoBuf;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

[Collection(ModInformationRoleCollection.Name)]
public class IssueOwnershipSnapshotReceiverTests : IDisposable
{
    static IssueOwnershipSnapshotReceiverTests()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(Coop.Tests.Mocks.TestNetwork).Module.ModuleHandle);
    }

    private readonly bool wasServer = ModInformation.IsServer;
    private readonly IssueOwnershipRegistry registry = new IssueOwnershipRegistry();
    private readonly MessageBroker broker = new MessageBroker();
    private readonly List<(string IssueOwnerId, string IssueId, bool WasOwner)> cancelled = new();
    private readonly IssueOwnershipSnapshotReceiver receiver;

    public IssueOwnershipSnapshotReceiverTests()
    {
        ModInformation.IsServer = false;
        registry.TrySetOwner("hero_1", "issue_1", "stale-player");
        var finalization = new Mock<IIssueFinalizationReplay>();
        finalization.Setup(replay => replay.ReplayLocalCancel(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Callback<string, string, bool>((ownerId, issueId, wasOwner) => cancelled.Add((ownerId, issueId, wasOwner)));
        receiver = new IssueOwnershipSnapshotReceiver(broker, registry,
            Mock.Of<IIssueResolver>(), Mock.Of<IControllerIdProvider>(c => c.ControllerId == "stale-player"), finalization.Object);
    }

    public void Dispose()
    {
        receiver.Dispose();
        ModInformation.IsServer = wasServer;
    }

    private void Receive(NetworkIssueOwnershipSnapshot snapshot)
    {
        Assert.False(GameThread.Instance.IsGameThread);
        GameThread.Run(() => receiver.Handle_NetworkIssueOwnershipSnapshot(
            new MessagePayload<NetworkIssueOwnershipSnapshot>(this, snapshot)), blocking: true);
    }

    private static NetworkIssueOwnershipSnapshot ThroughTheWire(NetworkIssueOwnershipSnapshot snapshot)
    {
        using (var stream = new MemoryStream())
        {
            Serializer.Serialize(stream, snapshot);
            stream.Position = 0;
            return Serializer.Deserialize<NetworkIssueOwnershipSnapshot>(stream);
        }
    }

    [Fact]
    public void ASnapshotWithoutAnOwnersArrayClearsWhatTheClientRemembered()
    {
        Receive(new NetworkIssueOwnershipSnapshot(null));

        Assert.Empty(registry.GetAll());
    }

    [Fact]
    public void ARemovedOwnerReplaysCancellationForAClientThatMissedIt()
    {
        Receive(new NetworkIssueOwnershipSnapshot(Array.Empty<IssueOwnershipData>()));

        var replayed = Assert.Single(cancelled);
        Assert.Equal("hero_1", replayed.IssueOwnerId);
        Assert.Equal("issue_1", replayed.IssueId);
        Assert.True(replayed.WasOwner);
    }

    [Fact]
    public void AChangedControllerDoesNotReplayCancellationForTheSameIssue()
    {
        Receive(new NetworkIssueOwnershipSnapshot(new[] { new IssueOwnershipData("hero_1", "issue_1", "new-player") }));

        Assert.True(registry.TryGetOwner("hero_1", "issue_1", out var owner));
        Assert.Equal("new-player", owner);
        Assert.Empty(cancelled);
    }

    [Fact]
    public void AnEmptySnapshotThatWentThroughTheWireClearsWhatTheClientRemembered()
    {
        var received = ThroughTheWire(new NetworkIssueOwnershipSnapshot(Array.Empty<IssueOwnershipData>()));

        Receive(received);

        Assert.Empty(registry.GetAll());
    }

    [Fact]
    public void ASnapshotReplacesWhatTheClientRememberedWithTheServersOwners()
    {
        var received = ThroughTheWire(new NetworkIssueOwnershipSnapshot(new[]
        {
            new IssueOwnershipData("hero_1", "issue_1", "player-A"),
            new IssueOwnershipData("hero_2", "issue_2", "player-B"),
        }));

        Receive(received);

        Assert.Equal(2, registry.GetAll().Length);
        Assert.True(registry.TryGetOwner("hero_1", "issue_1", out var first));
        Assert.Equal("player-A", first);
        Assert.True(registry.TryGetOwner("hero_2", "issue_2", out var second));
        Assert.Equal("player-B", second);
    }

    [Fact]
    public void AThrowingCancelKeepsTheReplacedMirrorAndDoesNotSkipOtherCancels()
    {
        registry.TrySetOwner("hero_2", "issue_2", "another-player");
        var replay = new Mock<IIssueFinalizationReplay>();
        replay.Setup(value => value.ReplayLocalCancel("hero_1", "issue_1", true)).Callback(() =>
        {
            Assert.False(registry.TryGetOwner("hero_1", "issue_1", out _));
            Assert.True(registry.TryGetOwner("hero_1", "replacement", out var owner));
            Assert.Equal("new-player", owner);
            throw new InvalidOperationException("cancel failed");
        });
        using var throwingReceiver = new IssueOwnershipSnapshotReceiver(new MessageBroker(), registry,
            Mock.Of<IIssueResolver>(), Mock.Of<IControllerIdProvider>(c => c.ControllerId == "stale-player"), replay.Object);

        GameThread.Run(() => throwingReceiver.Handle_NetworkIssueOwnershipSnapshot(
            new MessagePayload<NetworkIssueOwnershipSnapshot>(this, new NetworkIssueOwnershipSnapshot(new[]
            {
                new IssueOwnershipData("hero_1", "replacement", "new-player"),
            }))), blocking: true);

        replay.Verify(value => value.ReplayLocalCancel("hero_1", "issue_1", true), Times.Once);
        replay.Verify(value => value.ReplayLocalCancel("hero_2", "issue_2", false), Times.Once);
        Assert.Single(registry.GetAll());
    }

    [Fact]
    public void TheServerIgnoresASnapshot()
    {
        ModInformation.IsServer = true;

        Receive(new NetworkIssueOwnershipSnapshot(null));

        var remembered = Assert.Single(registry.GetAll());
        Assert.Equal("stale-player", remembered.ControllerId);
    }
}
