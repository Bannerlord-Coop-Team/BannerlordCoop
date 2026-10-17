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
using TaleWorlds.Core;
using TaleWorlds.Library;
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
                hero.Occupation = Occupation.GangLeader;
            }

            Assert.False(settlement.IsTown);

            Assert.False(GangLeaderCaptureStrategy().TryBuildDebugCapture(hero, out _));
        });
    }

    [Fact]
    public void GangLeaderDebugCapture_IsRefusedForAHeroThatIsNotANotable()
    {
        var heroId = CreateHeroInATownWithAHideout();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.False(hero.IsNotable);

            Assert.False(GangLeaderCaptureStrategy().TryBuildDebugCapture(hero, out _));
        });
    }

    [Fact]
    public void GangLeaderDebugCapture_IsBuiltForAGangLeaderInATown()
    {
        var heroId = CreateHeroInATownWithAHideout();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));

            using (new AllowedThread())
            {
                hero.Occupation = Occupation.GangLeader;
            }

            Assert.True(GangLeaderCaptureStrategy().TryBuildDebugCapture(hero, out var captured));
            Assert.NotEmpty(captured);
        });
    }

    private ICreationCaptureStrategy GangLeaderCaptureStrategy()
    {
        Assert.True(Server.Container.Resolve<IQuestTypeRegistry>().TryGetByName(GangLeaderIssueName, out var descriptor));
        return descriptor.CreationCaptureStrategy;
    }

    private string CreateHeroInATownWithAHideout()
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var townSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var townId = TestEnvironment.CreateRegisteredObject<Town>();
        var hideoutSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var hideoutId = TestEnvironment.CreateRegisteredObject<Hideout>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(townSettlementId, out var townSettlement));
            Assert.True(Server.ObjectManager.TryGetObject<Town>(townId, out var town));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(hideoutSettlementId, out var hideoutSettlement));
            Assert.True(Server.ObjectManager.TryGetObject<Hideout>(hideoutId, out var hideout));

            using (new AllowedThread())
            {
                townSettlement.Town = town;
                hero.StayingInSettlement = townSettlement;
                hideoutSettlement.SetSettlementComponent(hideout);

                Campaign.Current._hideouts ??= new MBList<Hideout>();
                Campaign.Current._hideouts.Add(hideout);
            }

            Assert.True(townSettlement.IsTown);
        });

        return heroId;
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
