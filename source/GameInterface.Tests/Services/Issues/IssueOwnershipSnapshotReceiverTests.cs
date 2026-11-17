using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Framework.Registries;
using ProtoBuf;
using Moq;
using System;
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
    private readonly IssueOwnershipSnapshotReceiver receiver;
    private readonly System.Collections.Generic.List<(string IssueOwnerId, string IssueId, bool WasOwner)> cancelled = new();
    private readonly System.Collections.Generic.List<Action> deferredCancels = new();

    public IssueOwnershipSnapshotReceiverTests()
    {
        ModInformation.IsServer = false;
        registry.TrySetOwner("hero_1", "issue_1", "stale-player");
        var finalization = new FakeFinalizationReplay((ownerId, issueId, wasOwner) =>
            deferredCancels.Add(() => cancelled.Add((ownerId, issueId, wasOwner))));
        receiver = new IssueOwnershipSnapshotReceiver(new MessageBroker(), registry,
            Mock.Of<IIssueResolver>(), Mock.Of<IControllerIdProvider>(c => c.ControllerId == "stale-player"), finalization);
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
        foreach (var cancel in deferredCancels) cancel();
        var replayed = Assert.Single(cancelled);
        Assert.Equal("hero_1", replayed.IssueOwnerId);
        Assert.Equal("issue_1", replayed.IssueId);
        Assert.True(replayed.WasOwner);
    }

    [Fact]
    public void AnEmptySnapshotThatWentThroughTheWireClearsWhatTheClientRemembered()
    {
        var received = ThroughTheWire(new NetworkIssueOwnershipSnapshot(Array.Empty<IssueOwnershipData>()));

        Receive(received);

        Assert.Empty(registry.GetAll());
    }

    [Fact]
    public void DeferredSnapshotCancelKeepsOwnershipCapturedBeforeTheMirrorIsRebuilt()
    {
        Receive(new NetworkIssueOwnershipSnapshot(new[]
        {
            new IssueOwnershipData("hero_1", "replacement", "other-player"),
        }));

        Assert.Empty(cancelled);
        Assert.True(registry.TryGetOwner("hero_1", "replacement", out var replacementOwner));
        Assert.Equal("other-player", replacementOwner);

        foreach (var cancel in deferredCancels) cancel();

        var replayed = Assert.Single(cancelled);
        Assert.Equal("hero_1", replayed.IssueOwnerId);
        Assert.Equal("issue_1", replayed.IssueId);
        Assert.True(replayed.WasOwner);
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
    public void TheServerIgnoresASnapshot()
    {
        ModInformation.IsServer = true;

        Receive(new NetworkIssueOwnershipSnapshot(null));

        var remembered = Assert.Single(registry.GetAll());
        Assert.Equal("stale-player", remembered.ControllerId);
    }

    private sealed class FakeFinalizationReplay : IIssueFinalizationReplay
    {
        private readonly Action<string, string, bool> replay;

        public FakeFinalizationReplay(Action<string, string, bool> replay) => this.replay = replay;

        public void ReplayLocalCancel(string issueOwnerId, string issueId, bool wasOwner) => replay(issueOwnerId, issueId, wasOwner);
    }
}
