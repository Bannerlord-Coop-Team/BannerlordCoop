using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment;
using GameInterface.Registry.Auto;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Party.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.TroopRosters.Data;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

public class AlternativeSolutionCommitmentsTests : IDisposable
{
    private readonly E2ETestEnvironment environment;

    public AlternativeSolutionCommitmentsTests(ITestOutputHelper output) => environment = new E2ETestEnvironment(output);

    public void Dispose() => environment.Dispose();

    [Fact]
    public void RepeatedSelectionsConsumptionAndRefundCreateNoRegisteredOrBroadcastRosters()
    {
        var server = environment.Server;
        var client = environment.Clients.First();
        var heroId = environment.CreateRegisteredObject<Hero>();
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        var troopId = environment.CreateRegisteredObject<CharacterObject>();
        server.Call(() => Assert.True(server.Resolve<IPlayerManager>().AddPlayer(new Player("player-A", heroId, partyId, "", ""))));
        environment.ConnectRegisteredPlayer(client, "player-A");
        uint troopHandle = 0;
        var initialCount = 0;
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(server.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            using (new AllowedThread()) hero.PartyBelongedTo = party;
            Assert.True(server.ObjectManager.TryGetHandle(troop, out troopHandle));
            initialCount = party.MemberRoster.GetTroopCount(troop);
        });
        var createdBefore = server.InternalMessages.GetMessages<InstanceCreated<TroopRoster>>().Count();
        var sentBefore = server.NetworkSentMessages.GetMessages<NetworkCreateInstance<TroopRoster>>().Count();
        server.Call(() =>
        {
            var commitments = server.Resolve<IAlternativeSolutionCommitments>();
            Assert.True(server.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            var selection = new[] { new TroopRosterElement(troop) { Number = 4, WoundedNumber = 1, Xp = 20 } };
            for (var i = 0; i < 200; i++)
            {
                commitments.SetSelection(client.NetPeer, "giver", "issue", selection);
                var consumed = Assert.Single(commitments.Consume(heroId, "giver", "issue", Array.Empty<TroopRosterElementData>()));
                Assert.Equal((4, 1, 20), (consumed.Number, consumed.WoundedNumber, consumed.Xp));
                commitments.SetSelection(client.NetPeer, "giver", "issue", selection);
                commitments.ReturnForIssue("giver", "issue");
            }
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.Equal(initialCount + 800, party.MemberRoster.GetTroopCount(troop));
        });
        environment.FlushCoalescer();

        Assert.Equal(createdBefore, server.InternalMessages.GetMessages<InstanceCreated<TroopRoster>>().Count());
        Assert.Equal(sentBefore, server.NetworkSentMessages.GetMessages<NetworkCreateInstance<TroopRoster>>().Count());
    }
}
