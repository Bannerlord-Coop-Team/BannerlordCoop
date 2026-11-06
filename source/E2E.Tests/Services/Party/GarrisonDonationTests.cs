using Common.Network;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.MapEventParties;
using GameInterface.Services.Party.Data;
using GameInterface.Services.Party.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.TroopRosters.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Party;

/// <summary>
/// Covers the garrison troop donation, which the server applies together with the donate screen's Done.
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

    // The Done moves the troops from the party to the garrison while the party is in the town,
    // and changes neither once the party left it, so the troops are never in both or lost.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DonationDone_MovesTroopsToGarrisonOnlyWhileThePartyIsInTheTown(bool inTown)
    {
        string settlementId = CreateTownSettlement();
        string characterId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        string heroId = RegisterDonor(inTown ? settlementId : null);
        Server.Call(() => Server.GetRegisteredObject<Hero>(heroId).PartyBelongedTo.MemberRoster
            .AddToCounts(Server.GetRegisteredObject<CharacterObject>(characterId), 3));

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

    // The Done a donate screen sends: the donated troops fill the empty left roster and leave the donor's own roster.
    private NetworkCompleteDoneLogic CreateDonationDone(string heroId, string settlementId, string characterId, int count)
    {
        uint characterHandle = Server.GetHandle<CharacterObject>(characterId);
        var emptyRoster = new TroopRosterData(Array.Empty<TroopRosterElementData>());
        var donatedDelta = new TroopRosterData(new[] { new TroopRosterElementData(characterHandle, count, 0, 0) });
        var donorDelta = new TroopRosterData(new[] { new TroopRosterElementData(characterHandle, -count, 0, 0) });

        return new NetworkCompleteDoneLogic(
            heroId,
            Array.Empty<FlattenedTroop>(),
            Array.Empty<FlattenedTroop>(),
            Array.Empty<FlattenedTroop>(),
            donatedDelta,
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

    // Sends the Done from the client over the test network.
    private void Send(NetworkCompleteDoneLogic message)
    {
        Client.Call(() => Client.Resolve<INetwork>().SendAll(message));
        TestEnvironment.FlushCoalescer();
    }

    // Tears down the test environment.
    public void Dispose() => TestEnvironment.Dispose();
}
