using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Party.Messages;
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
public class PartyDoneLogicInfluenceSyncTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

    public PartyDoneLogicInfluenceSyncTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    [Fact]
    public void ClientPartyDoneLogic_WithInfluenceGain_CreditsRequestingClan_OnAllClients()
    {
        const int influenceGain = 50;

        string heroId = null;
        string clanId = null;
        string partyId = null;
        float baselineInfluence = 0;

        // Arrange: a requesting player's party whose leader's clan sits in a kingdom (influence only applies to
        // a clan that has a kingdom). The party + clan + kingdom are created on the server and replicate.
        Server.Call(() =>
        {
            var party = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var kingdom = GameObjectCreator.CreateInitializedObject<Kingdom>();

            var hero = party.LeaderHero;
            hero.Clan.Kingdom = kingdom;

            // The server apply reads mainHero.PartyBelongedTo, so make sure the requesting hero belongs to its party.
            if (hero.PartyBelongedTo == null) hero.PartyBelongedTo = party;

            baselineInfluence = hero.Clan.Influence;

            Assert.True(Server.ObjectManager.TryGetId(party, out partyId));
            Assert.True(Server.ObjectManager.TryGetId(hero, out heroId));
            Assert.True(Server.ObjectManager.TryGetId(hero.Clan, out clanId));
        });

        // Act: a client finishes the party screen with a pending influence gain and nothing else changed. This
        // mirrors how PartyScreenLogicPatches builds the event; empty rosters keep the test focused on influence.
        var requestingClient = Clients.First();
        requestingClient.Call(() =>
        {
            Assert.True(requestingClient.ObjectManager.TryGetObject<MobileParty>(partyId, out var clientParty));
            Assert.True(requestingClient.ObjectManager.TryGetObject<Hero>(heroId, out var clientHero));

            var message = new PartyDoneLogicAttempted(
                clientHero,
                new FlattenedTroopRoster(4),
                new FlattenedTroopRoster(4),
                new FlattenedTroopRoster(4),
                leftMemberRoster: null,
                leftPrisonerRoster: null,
                rightMemberRoster: null,
                rightPrisonerRoster: null,
                initialLeftMemberRoster: null,
                initialLeftPrisonerRoster: null,
                initialRightMemberRoster: null,
                initialRightPrisonerRoster: null,
                clientParty.ItemRoster,
                new List<Tuple<CharacterObject, CharacterObject, int>>(),
                leftParty: null,
                partyGoldChangeAmount: 0,
                partyInfluenceChangeAmount: influenceGain,
                partyMoraleChangeAmount: 0,
                doNotApplyGoldTransactions: true);

            requestingClient.SimulateMessage(this, message);
        });

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
