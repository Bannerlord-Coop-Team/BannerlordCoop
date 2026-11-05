using TaleWorlds.CampaignSystem.Party;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Players;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEventParties;
using GameInterface.Services.Party.Data;
using GameInterface.Services.TroopRosters.Data;
using TaleWorlds.Core;
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
        RegisterDonor(settlementId);

        Send(new DonateToGarrison(settlementId, new List<DonateTroop> { new DonateTroop(characterId, 3) }));

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject(settlementId, out Settlement settlement));
            Assert.True(Server.ObjectManager.TryGetObject(characterId, out CharacterObject character));
            Assert.NotNull(settlement.Town.GarrisonParty);
            Assert.Equal(3, settlement.Town.GarrisonParty.MemberRoster.GetTroopCount(character));
        });
    }

    // A donation from a player whose party is not in the town is refused.
    [Fact]
    public void Donation_FromAPartyOutsideTheTown_IsRefused()
    {
        string settlementId = CreateTownSettlement();
        string characterId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        RegisterDonor(null);

        Send(new DonateToGarrison(settlementId, new List<DonateTroop> { new DonateTroop(characterId, 3) }));

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject(settlementId, out Settlement settlement));
            Assert.True(Server.ObjectManager.TryGetObject(characterId, out CharacterObject character));
            Assert.Equal(0, settlement.Town.GarrisonParty?.MemberRoster.GetTroopCount(character) ?? 0);
        });
    }

    // The donate screen's Done sends the garrison add and the party's roster change; both apply in the town,
    // and neither does once the party left it, so the troops are never lost on a stale screen.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DonationAndDone_ApplyTogetherOnlyWhileThePartyIsInTheTown(bool inTown)
    {
        string settlementId = CreateTownSettlement();
        string characterId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        string heroId = RegisterDonor(inTown ? settlementId : null);
        Server.Call(() => Server.GetRegisteredObject<Hero>(heroId).PartyBelongedTo.MemberRoster
            .AddToCounts(Server.GetRegisteredObject<CharacterObject>(characterId), 3));

        Send(new DonateToGarrison(settlementId, new List<DonateTroop> { new DonateTroop(characterId, 3) }));
        Send(CreateDonationDone(heroId, settlementId, characterId, 3));

        Server.Call(() =>
        {
            var settlement = Server.GetRegisteredObject<Settlement>(settlementId);
            var character = Server.GetRegisteredObject<CharacterObject>(characterId);
            int partyCount = Server.GetRegisteredObject<Hero>(heroId).PartyBelongedTo.MemberRoster.GetTroopCount(character);
            int garrisonCount = settlement.Town.GarrisonParty?.MemberRoster.GetTroopCount(character) ?? 0;
            Assert.Equal(inTown ? 0 : 3, partyCount);
            Assert.Equal(inTown ? 3 : 0, garrisonCount);
        });
    }

    // Managing the garrison moves the screen's heroes into the town only for a player whose clan owns it.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ManageGarrison_IsAppliedOnlyInTheSendersOwnClanTown(bool ownsTown)
    {
        string settlementId = CreateTownSettlement();
        RegisterDonor(settlementId);
        string heroId = null, memberRosterId = null, prisonerRosterId = null;
        Server.Call(() =>
        {
            var settlement = Server.GetRegisteredObject<Settlement>(settlementId);
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer("Donor", out var donor));
            if (ownsTown) Server.GetRegisteredObject<Hero>(donor.HeroId).Clan = settlement.OwnerClan;

            var leftParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            Assert.True(Server.ObjectManager.TryGetId(leftParty.LeaderHero, out heroId));
            Assert.True(Server.ObjectManager.TryGetId(leftParty.MemberRoster, out memberRosterId));
            Assert.True(Server.ObjectManager.TryGetId(leftParty.PrisonRoster, out prisonerRosterId));
        });

        Client.Call(() => Client.Resolve<INetwork>().SendAll(new DoManageGarrison(settlementId, memberRosterId, prisonerRosterId)));
        TestEnvironment.FlushCoalescer();
        Server.PumpGameThread();

        Server.Call(() => Assert.Equal(ownsTown,
            Server.GetRegisteredObject<Hero>(heroId).StayingInSettlement == Server.GetRegisteredObject<Settlement>(settlementId)));
    }

    // The donor is the client's player, with its party in the given town (or nowhere for null).
    private string RegisterDonor(string settlementId)
    {
        string donorHeroId = null;
        Server.Call(() =>
        {
            var party = GameObjectCreator.CreateInitializedObject<MobileParty>();
            if (settlementId != null) party._currentSettlement = Server.GetRegisteredObject<Settlement>(settlementId);
            Assert.True(Server.ObjectManager.TryGetId(party, out var partyId));
            Assert.True(Server.ObjectManager.TryGetId(party.LeaderHero, out var heroId));

            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("Donor", heroId, partyId, null, null)));
            playerManager.SetPeer("Donor", Client.NetPeer);
            donorHeroId = heroId;
        });
        return donorHeroId;
    }

    // The Done a donate screen sends: the donated troops leave the donor's own roster.
    private NetworkCompleteDoneLogic CreateDonationDone(string heroId, string settlementId, string characterId, int count)
    {
        var emptyRoster = new TroopRosterData(Array.Empty<TroopRosterElementData>());
        var donorDelta = new TroopRosterData(new[]
        {
            new TroopRosterElementData(Server.GetHandle<CharacterObject>(characterId), -count, 0, 0),
        });

        return new NetworkCompleteDoneLogic(
            heroId,
            Array.Empty<FlattenedTroop>(),
            Array.Empty<FlattenedTroop>(),
            Array.Empty<FlattenedTroop>(),
            emptyRoster,
            emptyRoster,
            donorDelta,
            emptyRoster,
            Array.Empty<ItemRosterElement>(),
            new UpgradedTroopHistoryData(new List<UpgradedTroopHistoryElementData>()),
            leftPartyId: null,
            leftPrisonerRosterId: null,
            partyGoldChangeAmount: 0,
            partyInfluenceChangeAmount: 0,
            partyMoraleChangeAmount: 0,
            doNotApplyGoldTransactions: true,
            default,
            Helpers.PartyScreenHelper.PartyScreenMode.TroopsManage,
            new TroopRosterOrderData(new()),
            garrisonDonationSettlementId: settlementId);
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

    // Sends a message from the client over the test network.
    private void Send(IMessage message)
    {
        Client.Call(() => Client.Resolve<INetwork>().SendAll(message));
        TestEnvironment.FlushCoalescer();
    }

    // Tears down the test environment.
    public void Dispose() => TestEnvironment.Dispose();
}
