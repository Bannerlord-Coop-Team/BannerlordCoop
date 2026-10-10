using Autofac;
using Common.Commands;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Issues.Framework.Commands;
using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

public class IssueSafetyTests : IDisposable
{
    private const string GangLeaderIssueName = "GangLeaderNeedsToOffloadStolenGoodsIssue";

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;

    public IssueSafetyTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void Give_ToAHeroThatIsNotANotable_IsRejected()
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            using (new AllowedThread())
            {
                hero.StayingInSettlement = settlement;
            }

            var command = new IssuesGiveCoopCommand(Server.Container.Resolve<IQuestTypeRegistry>(), Server.ObjectManager);
            var args = new CoopCommandArgsFactory().FromValues(new[] { heroId, GangLeaderIssueName });

            var result = command.ProcessCommand(args);

            Assert.False(result.Succeeded);
            Assert.Null(hero.Issue);
        });
    }

    [Fact]
    public void GangLeaderDebugCapture_IsRefusedOutsideATown()
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            using (new AllowedThread())
            {
                hero.StayingInSettlement = settlement;
            }

            Assert.False(settlement.IsTown);

            var registry = Server.Container.Resolve<IQuestTypeRegistry>();
            Assert.True(registry.TryGetByName(GangLeaderIssueName, out var descriptor));

            Assert.False(descriptor.CreationCaptureStrategy.TryBuildDebugCapture(hero, out _));
        });
    }
}
