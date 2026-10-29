using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssueQuest;

namespace E2E.Tests.Services.Issues;

/// <summary>
/// A quest war ending caused by crime rating is judged on the quest owner's rating, not the server main hero's (#3986).
/// </summary>
public class QuestWarCrimeRatingTests : IDisposable
{
    private const string IssueId = "issue_army_needs_supplies";

    private static readonly MethodBase[] WarLogTexts =
    {
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.PlayerDeclaredWarQuestLogText)),
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.QuestCanceledWarDeclaredLog)),
    };

    private readonly string lordId;
    private readonly string lordClanId;
    private readonly string kingdomId;
    private readonly string armyPartyId;
    private readonly string ownerClanId;
    private readonly string otherClanId;
    private readonly Player owner;
    private readonly Player other;
    private readonly StubIssuePreconditions preconditions = new(typeof(Issue));

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance ClientA => TestEnvironment.Clients.First();
    private EnvironmentInstance ClientB => TestEnvironment.Clients.Last();
    private EnvironmentInstance[] Instances => new[] { Server, ClientA, ClientB };

    public QuestWarCrimeRatingTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        lordId = TestEnvironment.CreateRegisteredObject<Hero>();
        lordClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        armyPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        ownerClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        otherClanId = TestEnvironment.CreateRegisteredObject<Clan>();

        owner = RegisterPlayer(ClientA, "player-A", ownerClanId);
        other = RegisterPlayer(ClientB, "player-B", otherClanId);

        foreach (var instance in Instances)
        {
            GiveTheArmyLeaderTheIssue(instance);
        }

        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(lordId, out var lord));
            lord.Issue.StartIssueWithQuest();
        });
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
        preconditions.Dispose();
    }

    [Theory]
    [InlineData(61f, 0f, IssueOutcome.QuestFail)]
    [InlineData(0f, 61f, IssueOutcome.QuestCancel)]
    public void ACrimeRatingWarIsJudgedOnTheQuestOwnersRating(float ownerRating, float otherRating, IssueOutcome outcome)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(ownerClanId, out var ownerClan));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(other.HeroId, out var otherHero));

            owner.CrimeRatings[kingdomId] = ownerRating;
            other.CrimeRatings[kingdomId] = otherRating;

            // The war is declared while another player is the server's main hero, so only the owner scope can pick the right rating
            using (new MainHeroSubstitutionScope(otherHero, otherHero.PartyBelongedTo))
            {
                DeclareWarAction.ApplyInternal(kingdom, ownerClan, DeclareWarAction.DeclareWarDetail.CausedByCrimeRatingChange);
            }
        }, WarLogTexts);

        var finalized = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(outcome, finalized.Outcome);
    }

    private Player RegisterPlayer(EnvironmentInstance client, string controllerId, string clanId)
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var player = new Player(controllerId, heroId, partyId, "", "");

        Server.Call(() => Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(player)));
        TestEnvironment.ConnectRegisteredPlayer(client, controllerId);
        client.Resolve<IControllerIdProvider>().SetControllerId(controllerId);

        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
                Assert.True(instance.ObjectManager.TryGetObject<Clan>(clanId, out var clan));

                using (new AllowedThread())
                {
                    hero.Clan = clan;
                }
            });
        }

        return player;
    }

    private void GiveTheArmyLeaderTheIssue(EnvironmentInstance instance)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(lordId, out var lord));
            Assert.True(instance.ObjectManager.TryGetObject<Clan>(lordClanId, out var lordClan));
            Assert.True(instance.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(armyPartyId, out var armyParty));

            using (new AllowedThread())
            {
                lord.Clan = lordClan;
                lordClan._kingdom = kingdom;
                lord._partyBelongedTo = armyParty;
                new Army(kingdom, armyParty, Army.ArmyTypes.Patrolling).ArmyOwner = lord;

                var issue = new Issue(lord);
                issue.StringId = IssueId;
                issue._issueState = IssueBase.IssueState.Ongoing;

                Campaign.Current.IssueManager._issues.Add(lord, issue);
                lord.OnIssueCreatedForHero(issue);
            }
        });
    }
}
