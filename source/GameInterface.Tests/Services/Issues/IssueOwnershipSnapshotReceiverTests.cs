using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Framework.Registries;
using ProtoBuf;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

[Collection(ModInformationRoleCollection.Name)]
public class IssueOwnershipSnapshotReceiverTests : IDisposable
{
    private readonly bool wasServer = ModInformation.IsServer;
    private readonly IssueOwnershipRegistry registry = new IssueOwnershipRegistry();
    private readonly IssueOwnershipSnapshotReceiver receiver;

    public IssueOwnershipSnapshotReceiverTests()
    {
        ModInformation.IsServer = false;
        registry.TrySetOwner("hero_1", "issue_1", "stale-player");
        receiver = new IssueOwnershipSnapshotReceiver(new MessageBroker(), registry);
    }

    public void Dispose()
    {
        receiver.Dispose();
        ModInformation.IsServer = wasServer;
    }

    private void Receive(NetworkIssueOwnershipSnapshot snapshot)
    {
        receiver.Handle_NetworkIssueOwnershipSnapshot(new MessagePayload<NetworkIssueOwnershipSnapshot>(this, snapshot));
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
    public void TheServerIgnoresASnapshot()
    {
        ModInformation.IsServer = true;

        Receive(new NetworkIssueOwnershipSnapshot(null));

        var remembered = Assert.Single(registry.GetAll());
        Assert.Equal("stale-player", remembered.ControllerId);
    }
}
