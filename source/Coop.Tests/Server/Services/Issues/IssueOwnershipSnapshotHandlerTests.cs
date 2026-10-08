using Common;
using Common.Messaging;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.Issues.Handlers;
using Coop.Tests.Mocks;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Framework.Registries;
using Moq;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace Coop.Tests.Server.Services.Issues;

[Collection(ModInformationRoleCollection.Name)]
public class IssueOwnershipSnapshotHandlerTests
{
    static IssueOwnershipSnapshotHandlerTests()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(TestNetwork).Module.ModuleHandle);
    }

    private static IssueOwnershipData[] Owners() => new[]
    {
        new IssueOwnershipData("hero_1", "issue_1", "player-A"),
        new IssueOwnershipData("hero_2", "issue_2", "player-B"),
    };

    private static void WithRole(bool isServer, System.Action test)
    {
        var wasServer = ModInformation.IsServer;
        ModInformation.IsServer = isServer;

        try
        {
            test();
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }

    [Fact]
    public void PlayerCampaignEntered_OnServer_SendsTheOwnershipSnapshotToThatPeerOnly()
    {
        WithRole(true, () =>
        {
            var messageBroker = new MessageBroker();
            var network = new TestNetwork();
            var enteringPeer = network.CreatePeer();
            var otherPeer = network.CreatePeer();
            var ownership = new Mock<IIssueOwnershipRegistry>();
            ownership.Setup(registry => registry.GetAll()).Returns(Owners());
            using var handler = new IssueOwnershipSnapshotHandler(messageBroker, network, ownership.Object);

            messageBroker.Publish(this, new PlayerCampaignEntered(enteringPeer));

            var snapshot = Assert.Single(network.GetPeerMessagesFromType<NetworkIssueOwnershipSnapshot>(enteringPeer));
            Assert.Equal(2, snapshot.Owners.Length);
            Assert.Contains(snapshot.Owners, owner => owner.IssueOwnerId == "hero_1" && owner.IssueId == "issue_1" && owner.ControllerId == "player-A");
            Assert.False(network.SentNetworkMessages.ContainsKey(otherPeer.Id));
        });
    }

    [Fact]
    public void PlayerCampaignEntered_OnServer_SendsNothingWhenNoIssueIsOwned()
    {
        WithRole(true, () =>
        {
            var messageBroker = new MessageBroker();
            var network = new TestNetwork();
            var enteringPeer = network.CreatePeer();
            var ownership = new Mock<IIssueOwnershipRegistry>();
            ownership.Setup(registry => registry.GetAll()).Returns(Enumerable.Empty<IssueOwnershipData>().ToArray());
            using var handler = new IssueOwnershipSnapshotHandler(messageBroker, network, ownership.Object);

            messageBroker.Publish(this, new PlayerCampaignEntered(enteringPeer));

            Assert.False(network.SentNetworkMessages.ContainsKey(enteringPeer.Id));
        });
    }

    [Fact]
    public void PlayerCampaignEntered_OnClient_SendsNothing()
    {
        WithRole(false, () =>
        {
            var messageBroker = new MessageBroker();
            var network = new TestNetwork();
            var enteringPeer = network.CreatePeer();
            var ownership = new Mock<IIssueOwnershipRegistry>();
            ownership.Setup(registry => registry.GetAll()).Returns(Owners());
            using var handler = new IssueOwnershipSnapshotHandler(messageBroker, network, ownership.Object);

            messageBroker.Publish(this, new PlayerCampaignEntered(enteringPeer));

            Assert.False(network.SentNetworkMessages.ContainsKey(enteringPeer.Id));
            ownership.Verify(registry => registry.GetAll(), Times.Never);
        });
    }
}
