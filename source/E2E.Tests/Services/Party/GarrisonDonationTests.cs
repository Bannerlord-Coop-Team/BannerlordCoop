using Common.Network;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Party.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Party;

/// <summary>
/// Covers the garrison troop donation round trip, including the empty donation that used to crash the dedicated server.
/// </summary>
public class GarrisonDonationTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Client => TestEnvironment.Clients.Single();

    public GarrisonDonationTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output, numClients: 1);
    }

    // Done on the donate screen with nothing moved must not send anything.
    [Fact]
    public void EmptyDonation_IsNotSentToServer()
    {
        string settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();

        PublishGarrisonDonated(settlementId, TroopRoster.CreateDummyTroopRoster());

        Assert.Empty(Client.NetworkSentMessages.GetMessages<DonateToGarrison>());
    }

    // Moved troops are sent to the server with their ids and counts.
    [Fact]
    public void Donation_IsSentToServer()
    {
        string settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        string characterId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        TroopRoster roster = TroopRoster.CreateDummyTroopRoster();
        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject(characterId, out CharacterObject character));
            roster.AddToCounts(character, 3);
        });

        PublishGarrisonDonated(settlementId, roster);

        var message = Assert.Single(Client.NetworkSentMessages.GetMessages<DonateToGarrison>());
        var troop = Assert.Single(message.Troops);
        Assert.Equal(characterId, troop.CharacterId);
        Assert.Equal(3, troop.Count);
    }

    // An empty donation from an older client arrives with null troops and must not touch the garrison.
    [Fact]
    public void EmptyDonation_ArrivesWithNullTroops_AndLeavesGarrisonUntouched()
    {
        string settlementId = CreateTownSettlement();

        Send(new DonateToGarrison(settlementId, new List<DonateTroop>()));

        // protobuf drops the empty list, so the server sees null rather than an empty list
        Assert.Null(Assert.Single(Server.InternalMessages.GetMessages<DonateToGarrison>()).Troops);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject(settlementId, out Settlement settlement));
            Assert.Null(settlement.Town.GarrisonParty);
        });
    }

    // A real donation creates the garrison and adds the troops on the server.
    [Fact]
    public void Donation_AddsTroopsToGarrison()
    {
        string settlementId = CreateTownSettlement();
        string characterId = TestEnvironment.CreateRegisteredObject<CharacterObject>();

        Send(new DonateToGarrison(settlementId, new List<DonateTroop> { new DonateTroop(characterId, 3) }));

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject(settlementId, out Settlement settlement));
            Assert.True(Server.ObjectManager.TryGetObject(characterId, out CharacterObject character));
            Assert.NotNull(settlement.Town.GarrisonParty);
            Assert.Equal(3, settlement.Town.GarrisonParty.MemberRoster.GetTroopCount(character));
        });
    }

    // Raises the client-side garrison donated event as the patched vanilla handler would.
    private void PublishGarrisonDonated(string settlementId, TroopRoster roster)
    {
        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject(settlementId, out Settlement settlement));
            Client.SimulateMessage(this, new GarrisonDonated(settlement, roster));
        });
        TestEnvironment.FlushCoalescer();
    }

    // Creates a server settlement with an owned town and no garrison yet.
    private string CreateTownSettlement()
    {
        string settlementId = null;
        Server.Call(() =>
        {
            var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            var town = GameObjectCreator.CreateInitializedObject<Town>();
            var ownerClan = GameObjectCreator.CreateInitializedObject<Clan>();
            settlement.Town = town;
            settlement.SetSettlementComponent(town);
            town.OwnerClan = ownerClan;
            town.IsOwnerUnassigned = false;

            Assert.True(Server.ObjectManager.TryGetId(settlement, out settlementId));
        });
        TestEnvironment.FlushCoalescer();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject(settlementId, out Settlement settlement));
            Assert.NotNull(settlement.Town);
        });
        return settlementId;
    }

    // Sends the donation from the client over the test network.
    private void Send(DonateToGarrison message)
    {
        Client.Call(() => Client.Resolve<INetwork>().SendAll(message));
        TestEnvironment.FlushCoalescer();
    }

    // Tears down the test environment.
    public void Dispose() => TestEnvironment.Dispose();
}
