using Autofac;
using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Issues.Framework.Dismissal;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Framework.Registries;
using GameInterface.Tests.Services.Issues;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Xunit.Abstractions;
using AllowedIssue = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue;
using UnregisteredIssue = E2E.Tests.Services.Issues.NeverRegisteredIssue;

namespace E2E.Tests.Services.Issues;

public class UnregisteredIssuePurgerTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;

    public UnregisteredIssuePurgerTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    private static void AddIssue(Hero owner, IssueBase issue)
    {
        using (new AllowedThread())
        {
            Campaign.Current.IssueManager._issues.Add(owner, issue);
            owner.OnIssueCreatedForHero(issue);
        }
    }

    [Fact]
    public void Purge_RemovesIdleIssuesOfUnregisteredTypesOnly()
    {
        var unregisteredOwnerId = TestEnvironment.CreateRegisteredObject<Hero>();
        var registeredOwnerId = TestEnvironment.CreateRegisteredObject<Hero>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(unregisteredOwnerId, out var unregisteredOwner));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(registeredOwnerId, out var registeredOwner));

            using (new AllowedThread())
            {
                AddIssue(unregisteredOwner, new UnregisteredIssue(unregisteredOwner));
                AddIssue(registeredOwner, new AllowedIssue(registeredOwner, null));
            }

            Assert.NotNull(unregisteredOwner.Issue);
            Assert.NotNull(registeredOwner.Issue);

            var purger = new UnregisteredIssuePurger(
                Server.Container.Resolve<IMessageBroker>(),
                Server.Container.Resolve<IQuestTypeRegistry>(),
                Server.Container.Resolve<IQuestFeatureFlag>(),
                Server.Container.Resolve<IIssueDismisser>());
            purger.Purge();
            purger.Dispose();

            Assert.Null(unregisteredOwner.Issue);
            Assert.NotNull(registeredOwner.Issue);
            Assert.DoesNotContain(unregisteredOwner, Campaign.Current.IssueManager.Issues.Keys);
        });
    }

    [Fact]
    public void Purge_KeepsTakenIssuesOfUnregisteredTypes_WhenQuestsAreOn()
    {
        var ownerId = TestEnvironment.CreateRegisteredObject<Hero>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownerId, out var owner));

            var issue = new UnregisteredIssue(owner);
            AddIssue(owner, issue);
            issue._issueState = IssueBase.IssueState.SolvingWithQuestSolution;

            var purger = new UnregisteredIssuePurger(
                Server.Container.Resolve<IMessageBroker>(),
                Server.Container.Resolve<IQuestTypeRegistry>(),
                TestQuestFeatureFlag.On,
                Server.Container.Resolve<IIssueDismisser>());
            purger.Purge();
            purger.Dispose();

            Assert.Same(issue, owner.Issue);
        });
    }

    [Fact]
    public void Purge_DismissesTakenIssuesAndRemovesIdleOnes_WhenQuestsAreOff()
    {
        var questOwnerId = TestEnvironment.CreateRegisteredObject<Hero>();
        var troopsOwnerId = TestEnvironment.CreateRegisteredObject<Hero>();
        var companionId = TestEnvironment.CreateRegisteredObject<Hero>();
        var idleOwnerId = TestEnvironment.CreateRegisteredObject<Hero>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(questOwnerId, out var questOwner));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(troopsOwnerId, out var troopsOwner));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(idleOwnerId, out var idleOwner));

            var questIssue = new UnregisteredIssue(questOwner);
            AddIssue(questOwner, questIssue);
            questIssue._issueState = IssueBase.IssueState.SolvingWithQuestSolution;

            var troopsIssue = new AllowedIssue(troopsOwner, null);
            AddIssue(troopsOwner, troopsIssue);
            troopsIssue._issueState = IssueBase.IssueState.SolvingWithAlternativeSolution;

            using (new AllowedThread())
            {
                troopsIssue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
                companion.ChangeState(Hero.CharacterStates.Disabled);
            }

            AddIssue(idleOwner, new AllowedIssue(idleOwner, null));

            var questsOff = TestQuestFeatureFlag.Off;
            var registry = new QuestTypeRegistry(Server.Container.Resolve<IEnumerable<IQuestTypeDescriptor>>(), questsOff);
            var purger = new UnregisteredIssuePurger(
                Server.Container.Resolve<IMessageBroker>(),
                registry,
                questsOff,
                Server.Container.Resolve<IIssueDismisser>());

            purger.Purge();
            purger.Dispose();

            Assert.Null(questOwner.Issue);
            Assert.Null(troopsOwner.Issue);
            Assert.Null(idleOwner.Issue);
            Assert.DoesNotContain(questOwner, Campaign.Current.IssueManager.Issues.Keys);
            Assert.DoesNotContain(troopsOwner, Campaign.Current.IssueManager.Issues.Keys);
            Assert.DoesNotContain(idleOwner, Campaign.Current.IssueManager.Issues.Keys);
            Assert.Equal(Hero.CharacterStates.Active, companion.HeroState);
        });
    }
}
