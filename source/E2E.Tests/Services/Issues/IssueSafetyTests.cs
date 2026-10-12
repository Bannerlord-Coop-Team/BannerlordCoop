using Autofac;
using Common.Commands;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Issues.Framework.Commands;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;
using ArmyNeedsSuppliesIssue = TaleWorlds.CampaignSystem.Issues.ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssue;

namespace E2E.Tests.Services.Issues;

public class IssueSafetyTests : IDisposable
{
    private const string GangLeaderIssueName = "GangLeaderNeedsToOffloadStolenGoodsIssue";
    private const string ArmyNeedsSuppliesIssueName = "ArmyNeedsSuppliesIssue";

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
    public void Give_ToAnArmyLeaderThatIsNotANotable_CreatesTheIssue()
    {
        var (heroId, _) = CreateArmyLeader();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.False(hero.IsNotable);

            var result = Give(heroId, ArmyNeedsSuppliesIssueName);

            Assert.True(result.Succeeded);
            Assert.IsType<ArmyNeedsSuppliesIssue>(hero.Issue);
        });
    }

    [Fact]
    public void Give_ToAPlayerHero_IsRejected()
    {
        var (heroId, partyId) = CreateArmyLeader();

        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player("player-A", heroId, partyId, "", "")));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));

            var result = Give(heroId, ArmyNeedsSuppliesIssueName);

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

    private CoopCommandResult Give(string heroId, string questType)
    {
        var command = new IssuesGiveCoopCommand(Server.Container.Resolve<IQuestTypeRegistry>(), Server.ObjectManager);
        var args = new CoopCommandArgsFactory().FromValues(new[] { heroId, questType });

        return command.ProcessCommand(args);
    }

    private (string HeroId, string PartyId) CreateArmyLeader()
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));

            using (new AllowedThread())
            {
                hero._partyBelongedTo = party;
                var army = new Army(kingdom, party, Army.ArmyTypes.Patrolling);
                army.ArmyOwner = hero;
            }
        });

        return (heroId, partyId);
    }
}
