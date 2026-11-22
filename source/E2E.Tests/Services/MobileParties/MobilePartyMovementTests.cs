using Autofac;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Villages.Interfaces;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Definition;
using TaleWorlds.SaveSystem.Load;
using TaleWorlds.SaveSystem.Save;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MobileParties;

public class MobilePartyMovementTests : SyncTestBase
{
    private readonly string MobilePartyId = "TestParty";
    private readonly string TargetPartyId = "TargetParty";
    private readonly string TargetSettlementId = "TargetSettlement";

    public MobilePartyMovementTests(ITestOutputHelper output) : base(output)
    {
        MobilePartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        TargetPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        TargetSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();

        var clientNum = 1;

        foreach (var client in TestEnvironment.Clients)
        {
            client.Container.Resolve<IControllerIdProvider>().SetControllerId($"TestClient{clientNum++}");
        }
    }

    [Fact]
    public void Party_SetMoveHold_Sync()
    {
        // Arrange
        var point = new Vec2(0.1f, 0.2f);
        var campaignPoint = new CampaignVec2(point, true);

        var server = TestEnvironment.Server;

        // Act
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var serverParty));
            serverParty.SetMoveGoToPoint(campaignPoint, MobileParty.NavigationType.Default);
            serverParty.SetMoveModeHold();
            TestEnvironment.FlushCoalescer();
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));
            AssertPartyMovementValues(client, clientParty);
        }
    }

    [Fact]
    public void Party_SetMoveEngageParty_Sync()
    {
        // Arrange
        var dt = 0.1f;
        var point = new Vec2(0.1f, 0.2f);
        var campaignPoint = new CampaignVec2(point, true);

        var server = TestEnvironment.Server;


        // Act
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var serverParty));
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(TargetPartyId, out var targetParty));
            serverParty.SetMoveEngageParty(targetParty, MobileParty.NavigationType.Default);
            serverParty.Ai.Tick(dt);
            TestEnvironment.FlushCoalescer();
        });

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));
                clientParty.Ai.Tick(dt);
            });
        }

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));
            AssertPartyMovementValues(client, clientParty);
        }
    }

    [Fact]
    public void Party_SetMoveGoToSettlement_Sync()
    {
        // Arrange
        var dt = 0.1f;
        var point = new Vec2(0.1f, 0.2f);
        var campaignPoint = new CampaignVec2(point, true);

        var server = TestEnvironment.Server;

        Assert.True(server.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var serverParty));

        // Act
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<Settlement>(TargetSettlementId, out var targetSettlement));
            serverParty.SetMoveGoToSettlement(targetSettlement, MobileParty.NavigationType.Default, false);
            serverParty.Ai.Tick(dt);
            TestEnvironment.FlushCoalescer();
        });

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));
                clientParty.Ai.Tick(dt);
            });
        }

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));
            AssertPartyMovementValues(client, clientParty);
        }
    }

    [Fact]
    public void PartyAi_SetMoveGoToPoint_Sync()
    {
        // Arrange
        var dt = 0.1f;
        var point = new Vec2(0.1f, 0.2f);
        var campaignPoint = new CampaignVec2(point, true);

        var server = TestEnvironment.Server;

        Assert.True(server.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var serverParty));

        // Act
        server.Call(() =>
        {
            
            serverParty.SetMoveGoToPoint(campaignPoint, MobileParty.NavigationType.Default);
            serverParty.Ai.Tick(dt);
            TestEnvironment.FlushCoalescer();
        });

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));
                clientParty.Ai.Tick(dt);
            });
        }

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));
            AssertPartyMovementValues(client, clientParty);
        }
    }

    [Fact]
    public void Peace_HoldsAiPartyWithLongRangePursuit()
    {
        var pursuingClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var targetClanId = TestEnvironment.CreateRegisteredObject<Clan>();

        TestEnvironment.Server.Call(() =>
        {
            Assert.True(TestEnvironment.Server.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var pursuingParty));
            Assert.True(TestEnvironment.Server.ObjectManager.TryGetObject<MobileParty>(TargetPartyId, out var targetParty));
            Assert.True(TestEnvironment.Server.ObjectManager.TryGetObject<Clan>(pursuingClanId, out var pursuingClan));
            Assert.True(TestEnvironment.Server.ObjectManager.TryGetObject<Clan>(targetClanId, out var targetClan));

            pursuingParty.ActualClan = pursuingClan;
            targetParty.ActualClan = targetClan;
            VillageHostileFactionStanceHelper.ApplyWarStance(pursuingClan, targetClan);
            pursuingParty.SetMoveGoAroundParty(targetParty, MobileParty.NavigationType.Default);

            Assert.True(FactionManager.IsAtWarAgainstFaction(pursuingClan, targetClan));
            Assert.Equal(AiBehavior.GoAroundParty, pursuingParty.DefaultBehavior);
            Assert.Equal(targetParty, pursuingParty.TargetParty);

            MakePeaceAction.Apply(pursuingClan, targetClan);

            Assert.False(FactionManager.IsAtWarAgainstFaction(pursuingClan, targetClan));
            Assert.Equal(AiBehavior.Hold, pursuingParty.DefaultBehavior);
            Assert.Equal(AiBehavior.Hold, pursuingParty.ShortTermBehavior);
            Assert.Equal(MoveModeType.Hold, pursuingParty.PartyMoveMode);
            Assert.Null(pursuingParty.TargetParty);
            Assert.Null(pursuingParty.MoveTargetParty);

            TestEnvironment.FlushCoalescer();
        });

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var pursuingParty));
                Assert.Equal(AiBehavior.Hold, pursuingParty.DefaultBehavior);
                Assert.Equal(AiBehavior.Hold, pursuingParty.ShortTermBehavior);
                Assert.Equal(MoveModeType.Hold, pursuingParty.PartyMoveMode);
                Assert.Null(pursuingParty.TargetParty);
                Assert.Null(pursuingParty.MoveTargetParty);
            });
        }
    }

    [Fact]
    public void MapUpdate_RestoresPursuitTargetAfterLoad()
    {
        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(MobilePartyId);
            var target = Server.GetRegisteredObject<MobileParty>(TargetPartyId);
            target.Position = new CampaignVec2(new Vec2(4f, 5f), true);
            party.SetMoveGoAroundParty(target, MobileParty.NavigationType.Default);

            var definitions = new DefinitionContext();
            definitions.FillWithCurrentTypes();
            Assert.False(definitions.GotError, string.Join("\n", definitions.Errors));

            var metadata = new MetaData();
            var save = new SaveContext(definitions);
            Assert.True(save.Save(party, metadata, out var error), error);
            var driver = new InMemDriver();
            Assert.Equal(SaveResult.Success,
                driver.Save("pursuing_party", 1, metadata, save.SaveData).GetAwaiter().GetResult().SaveResult);

            var load = new LoadContext(definitions, driver);
            Assert.True(load.Load(driver.Load("pursuing_party"), loadAsLateInitialize: true));
            var loadedParty = Assert.IsType<MobileParty>(load.RootObject);
            Assert.Equal(AiBehavior.GoAroundParty, loadedParty.DefaultBehavior);
            Assert.Null(loadedParty.Ai.AiBehaviorInteractable);
            var loadedTarget = Assert.IsType<MobileParty>(loadedParty.TargetParty);
            // Map repair updates the target's position before recalculating the pursuit destination.
            loadedTarget._position = new CampaignVec2(new Vec2(7f, 7f), true);

            loadedParty.CheckAiForMapChangeAndUpdateIfNeeded();

            Assert.Equal(loadedTarget.Position, loadedParty.MoveTargetPoint);
            Assert.Equal(loadedTarget.Position, loadedParty.Ai.BehaviorTarget);
        });
    }

    private void AssertPartyMovementValues(
        EnvironmentInstance client,
        MobileParty clientParty)
    {
        var server = TestEnvironment.Server;
        Assert.True(server.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var serverParty));

        Assert.Equal(serverParty.DefaultBehavior, clientParty.DefaultBehavior);
        Assert.Equal(serverParty.ShortTermBehavior, clientParty.ShortTermBehavior);
        Assert.Equal(serverParty.TargetPosition, clientParty.TargetPosition);
        Assert.Equal(serverParty.MoveTargetPoint, clientParty.MoveTargetPoint);
        Assert.Equal(serverParty.DesiredAiNavigationType, clientParty.DesiredAiNavigationType);
        Assert.Equal(serverParty.IsTargetingPort, clientParty.IsTargetingPort);
        Assert.Equal(serverParty.PartyMoveMode, clientParty.PartyMoveMode);
        Assert.Equal(serverParty.Ai.BehaviorTarget, clientParty.Ai.BehaviorTarget);

        AssertSameReference(client, serverParty.TargetParty, clientParty.TargetParty);
        AssertSameReference(client, serverParty.TargetSettlement, clientParty.TargetSettlement);
        AssertSameReference(client, serverParty.MoveTargetParty, clientParty.MoveTargetParty);
        AssertSameReference(client, serverParty.Ai.AiBehaviorPartyBase, clientParty.Ai.AiBehaviorPartyBase);
        if (serverParty.Ai.AiBehaviorInteractable is PartyBase serverInteractable)
            AssertSameReference(
                client,
                serverInteractable,
                Assert.IsType<PartyBase>(clientParty.Ai.AiBehaviorInteractable));
        else
            Assert.Null(clientParty.Ai.AiBehaviorInteractable);
    }

    private void AssertSameReference<T>(EnvironmentInstance client, T serverValue, T clientValue)
        where T : class
    {
        if (serverValue == null)
        {
            Assert.Null(clientValue);
            return;
        }

        Assert.NotNull(clientValue);
        Assert.True(TestEnvironment.Server.ObjectManager.TryGetId(serverValue, out var serverId));
        Assert.True(client.ObjectManager.TryGetId(clientValue, out var clientId));
        Assert.Equal(serverId, clientId);
    }
}
