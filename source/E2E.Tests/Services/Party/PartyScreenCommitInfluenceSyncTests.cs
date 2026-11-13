using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using Common.Network;
using GameInterface.Services.Party.Data;
using GameInterface.Services.Party.Messages;
using GameInterface.Services.TroopRosters.Data;
using TaleWorlds.CampaignSystem.Settlements;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Party;

/// <summary>
/// Regression test for the party-screen "done" flow's influence change (#1433). When a client closes the
/// party screen with a pending influence gain (e.g. leaving troops in a garrison), the server must credit the
/// influence to the REQUESTING player's clan - the hero resolved from the message - and replicate it to every
/// client. The bug applied it to <see cref="Hero.MainHero"/> (the local machine's hero), which is null on the
/// dedicated server - so the influence either threw and was dropped, or landed on the wrong clan.
/// </summary>
public class PartyScreenCommitInfluenceSyncTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

    public PartyScreenCommitInfluenceSyncTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    [Fact]
    public void GarrisonDonation_CreditsRequestingClanInfluence_OnAllClients()
    {
        string heroId = null;
        string clanId = null;
        string partyId = null;
        float baselineInfluence = 0;

        string troopId = null;

        // Arrange: a requesting player's party, inside a town, whose leader's clan sits in a kingdom (influence
        // only applies to a clan that has a kingdom). Everything is created on the server and replicates.
        Server.Call(() =>
        {
            var party = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var kingdom = GameObjectCreator.CreateInitializedObject<Kingdom>();
            var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            var town = GameObjectCreator.CreateInitializedObject<Town>();
            var troop = GameObjectCreator.CreateInitializedObject<CharacterObject>();

            var hero = party.LeaderHero;
            hero.Clan.Kingdom = kingdom;

            // The server apply reads mainHero.PartyBelongedTo, so make sure the requesting hero belongs to its party.
            if (hero.PartyBelongedTo == null) hero.PartyBelongedTo = party;

            settlement.Town = town;
            settlement.SetSettlementComponent(town);
            town.OwnerClan = hero.Clan;
            town.IsOwnerUnassigned = false;
            party.CurrentSettlement = settlement;
            troop.Level = 26;
            party.MemberRoster.AddToCounts(troop, 100);

            baselineInfluence = hero.Clan.Influence;

            Assert.True(Server.ObjectManager.TryGetId(party, out partyId));
            Assert.True(Server.ObjectManager.TryGetId(hero, out heroId));
            Assert.True(Server.ObjectManager.TryGetId(hero.Clan, out clanId));
            Assert.True(Server.ObjectManager.TryGetId(troop, out troopId));

            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("RequestingControllerId", heroId, string.Empty, string.Empty, string.Empty)));
            playerManager.SetPeer("RequestingControllerId", Clients.First().NetPeer);
        });
        TestEnvironment.FlushCoalescer();

        // Act: a client donates the troops to the town's garrison, which pays troop donation influence.
        uint troopHandle = Server.GetHandle<CharacterObject>(troopId);
        var requestingClient = Clients.First();
        requestingClient.Call(() =>
        {
            var donate = PartyScreenCommits.Transfer(
                PartyScreenLogic.PartyRosterSide.Right, PartyScreenLogic.TroopType.Member, troopHandle, 100);

            requestingClient.Resolve<INetwork>().SendAll(PartyScreenCommits.Create(
                heroId,
                Helpers.PartyScreenHelper.PartyScreenMode.TroopsManage,
                PartyScreenDoneKind.DonateGarrison,
                new[] { donate },
                isDonating: true));
        });
        TestEnvironment.FlushCoalescer();

        // Assert: the requesting clan - not the host's Hero.MainHero - was credited, and every client
        // converged on the server's value. We assert the increase and the server<->client match rather than an
        // exact post-value, so the test stays robust to vanilla's influence scaling (e.g. kingdom policies).
        float serverInfluence = 0;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(clanId, out var serverClan));
            serverInfluence = serverClan.Influence;
            Assert.True(serverInfluence > baselineInfluence,
                $"Requesting clan influence did not increase (baseline {baselineInfluence}, now {serverInfluence}).");
        });

        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Clan>(clanId, out var clientClan));
            Assert.Equal(serverInfluence, clientClan.Influence);
        }
    }

    public void Dispose() => TestEnvironment.Dispose();
}
