using Autofac;
using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Issues.Framework.Gating;
using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Xunit.Abstractions;
using AllowedIssue = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue;
using UnregisteredIssue = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue;

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

            var purger = new UnregisteredIssuePurger(Server.Container.Resolve<IMessageBroker>(), Server.Container.Resolve<IQuestTypeRegistry>());
            purger.Purge();
            purger.Dispose();

            Assert.Null(unregisteredOwner.Issue);
            Assert.NotNull(registeredOwner.Issue);
            Assert.DoesNotContain(unregisteredOwner, Campaign.Current.IssueManager.Issues.Keys);
        });
    }
}
