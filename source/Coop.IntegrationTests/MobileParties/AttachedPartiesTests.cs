using Common.Util;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.MobileParties.Messages;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.IntegrationTests.MobileParties;

public class AttachedPartiesTests
{
    // Creates a test environment with 1 server and 2 clients by default
    internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

    /// <summary>
    /// Test when the server receives an ReceivesAttachedPartyAdded event, the clients are commanded to change the value
    /// </summary>
    [Fact]
    public void ServerReceivesAttachedPartyAdded_PublishesAddAttachedParty_AllClients()
    {
        // Arrange
        var party1Id = "Party1";
        var party1 = ObjectHelper.SkipConstructor<MobileParty>();
        party1.StringId = party1Id;
        party1._attachedParties = new TaleWorlds.Library.MBList<MobileParty>();
        TestEnvironment.RegisterObjectInNetwork(party1, party1Id);

        var party2Id = "Party2";
        var party2 = ObjectHelper.SkipConstructor<MobileParty>();
        party2.StringId = party2Id;
        party2._attachedParties = new TaleWorlds.Library.MBList<MobileParty>();
        TestEnvironment.RegisterObjectInNetwork(party2, party2Id);

        var triggerMessage = new AttachedPartyAdded(party1, party2);

        var server = TestEnvironment.Server;

        // Act
        server.SimulateMessage(this, triggerMessage);

        // Assert
        // Verify the server sends a single message to it's network
        Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkAddAttachedParty>());

        // Verify the all clients send a single message to their game interfaces
        foreach (EnvironmentInstance client in TestEnvironment.Clients)
        {
            Assert.Equal(1, client.InternalMessages.GetMessageCount<AddAttachedParty>());
        }
    }


    /// <summary>
    /// Test when the server receives an AttachedPartyRemove event, the clients are commanded to change the value
    /// </summary>
    [Fact]
    public void ServerReceivesAttachedPartyRemoved_PublishesRemoveAttachedParty_AllClients()
    {
        // Arrange
        var party1Id = "Party1";
        var party1 = ObjectHelper.SkipConstructor<MobileParty>();
        party1.StringId = party1Id;
        party1._attachedParties = new TaleWorlds.Library.MBList<MobileParty>();
        TestEnvironment.RegisterObjectInNetwork(party1, party1Id);

        var party2Id = "Party2";
        var party2 = ObjectHelper.SkipConstructor<MobileParty>();
        party2.StringId = party2Id;
        party2._attachedParties = new TaleWorlds.Library.MBList<MobileParty>();
        TestEnvironment.RegisterObjectInNetwork(party2, party2Id);

        var triggerMessage = new AttachedPartyRemoved(party1, party2);

        var server = TestEnvironment.Server;

        // Act
        server.SimulateMessage(this, triggerMessage);

        // Assert
        // Verify the server sends a single message to it's network
        Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkRemoveAttachedParty>());

        // Verify the all clients send a single message to their game interfaces
        foreach (EnvironmentInstance client in TestEnvironment.Clients)
        {
            Assert.Equal(1, client.InternalMessages.GetMessageCount<RemoveAttachedParty>());
        }
    }
}
