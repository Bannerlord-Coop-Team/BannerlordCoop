using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;
using AllowedIssue = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue;

namespace E2E.Tests.Services.Issues;

public class AlternativeSolutionRejectTests : IDisposable
{
    private const string IssueId = "issue_reject";
    private const string ControllerId = "player-A";
    private const int KeptTroops = 2;
    private const int SentTroops = 10;

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Client => TestEnvironment.Clients.First();

    public AlternativeSolutionRejectTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    private static IssueBase AddIssue(EnvironmentInstance instance, string notableId, IssueBase.IssueState state)
    {
        IssueBase issue = null!;

        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(notableId, out var notable));

            using (new AllowedThread())
            {
                issue = new AllowedIssue(notable, null);
                issue.StringId = IssueId;
                issue._issueState = state;

                Campaign.Current.IssueManager._issues.Add(notable, issue);
                notable.OnIssueCreatedForHero(issue);
            }
        });

        return issue;
    }

    [Fact]
    public void RejectedAccept_PutsTheSentTroopsBackInTheOwnersPartyAndTheCompanionBackOnTheClient()
    {
        var notableId = TestEnvironment.CreateRegisteredObject<Hero>();
        var playerHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var companionId = TestEnvironment.CreateRegisteredObject<Hero>();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();

        Server.Call(() => Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player(ControllerId, playerHeroId, partyId, "", ""))));
        TestEnvironment.ConnectRegisteredPlayer(Client, ControllerId);

        var heroesInParty = 0;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(playerHeroId, out var playerHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));

            party.MemberRoster.AddToCounts(playerHero.CharacterObject, 1);
            party.MemberRoster.AddToCounts(companion.CharacterObject, 1);
            party.MemberRoster.AddToCounts(troop, KeptTroops);

            heroesInParty = party.MemberRoster.TotalHeroes;
        });
        TestEnvironment.FlushCoalescer();

        AddIssue(Server, notableId, IssueBase.IssueState.SolvingWithQuestSolution);
        var clientIssue = AddIssue(Client, notableId, IssueBase.IssueState.Ongoing);

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));

            using (new AllowedThread())
            {
                Campaign.Current.MainParty = party;
                party.MemberRoster.AddToCounts(companion.CharacterObject, -1);
                clientIssue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
                clientIssue.AlternativeSolutionSentTroops.AddToCounts(troop, SentTroops);
            }

            Assert.True(party.MemberRoster.FindIndexOfTroop(companion.CharacterObject) < 0);

            Client.Resolve<IMessageBroker>().Publish(this, new AlternativeSolutionAcceptRequested(clientIssue));
        });
        TestEnvironment.FlushCoalescer();

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkAlternativeSolutionAcceptRejected>());

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));

            var troopIndex = party.MemberRoster.FindIndexOfTroop(troop);
            Assert.Equal(KeptTroops + SentTroops, party.MemberRoster.GetElementNumber(troopIndex));
            Assert.Equal(1, party.MemberRoster.GetElementNumber(party.MemberRoster.FindIndexOfTroop(companion.CharacterObject)));
            Assert.Equal(heroesInParty, party.MemberRoster.TotalHeroes);
        });

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));

            Assert.Equal(0, clientIssue.AlternativeSolutionSentTroops.TotalManCount);
            Assert.Equal(1, party.MemberRoster.GetElementNumber(party.MemberRoster.FindIndexOfTroop(companion.CharacterObject)));
            Assert.Equal(KeptTroops + SentTroops, party.MemberRoster.GetElementNumber(party.MemberRoster.FindIndexOfTroop(troop)));
        });
    }
}
