using Autofac;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Entity;
using HarmonyLib;
using SandBox;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
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
    public void Party_SetMoveGoAroundParty_Sync()
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
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(TargetPartyId, out var targetParty));
            serverParty.SetMoveGoAroundParty(targetParty, MobileParty.NavigationType.Default);
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
    public void Party_SetMoveToNearestLand_Sync()
    {
        // Arrange
        var dt = 0.1f;
        var point = new Vec2(0.1f, 0.2f);
        var campaignPoint = new CampaignVec2(point, true);

        var server = TestEnvironment.Server;

        Assert.True(server.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var serverParty));
        Assert.True(server.ObjectManager.TryGetObject<Settlement>(TargetSettlementId, out var targetSettlement));

        // Act
        server.Call(() =>
        {
            serverParty.SetMoveToNearestLand(targetSettlement);
            serverParty.SetShortTermBehavior(AiBehavior.AssaultSettlement, targetSettlement.Party);
            serverParty.Ai.Tick(dt);
            TestEnvironment.FlushCoalescer();
        }, disabledMethods: new MethodBase[] { 
            AccessTools.Method(typeof(MapScene), nameof(MapScene.GetNearestFaceCenterForPositionWithPath))
        });

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));

            client.Call(() =>
            {
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
    public void Party_SetMoveGoToInteractablePoint_Sync()
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
            serverParty.SetMoveGoToInteractablePoint(targetSettlement.Party, MobileParty.NavigationType.Default);
            serverParty.Ai.Tick(dt);
            TestEnvironment.FlushCoalescer();
        });

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));

            client.Call(() =>
            {
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
    public void Party_SetMoveEscortParty_Sync()
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
            serverParty.SetMoveEscortParty(targetParty, MobileParty.NavigationType.Default, false);
            serverParty.Ai.Tick(dt);
            TestEnvironment.FlushCoalescer();
        });

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));

            client.Call(() =>
            {
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
    public void Party_SetMovePatrolAroundPoint_Sync()
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
            serverParty.SetMovePatrolAroundPoint(campaignPoint, MobileParty.NavigationType.Default);
            serverParty.Ai.Tick(dt);
            TestEnvironment.FlushCoalescer();
        });

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));

            client.Call(() =>
            {
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
    public void Party_SetMovePatrolAroundSettlement_Sync()
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
            Assert.True(server.ObjectManager.TryGetObject<Settlement>(TargetSettlementId, out var targetSettlement));
            serverParty.SetMovePatrolAroundSettlement(targetSettlement, MobileParty.NavigationType.Default, false);
            serverParty.Ai.Tick(dt);
            TestEnvironment.FlushCoalescer();
        });

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));

            client.Call(() =>
            {
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
    public void Party_SetMoveRaidSettlement_Sync()
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
            Assert.True(server.ObjectManager.TryGetObject<Settlement>(TargetSettlementId, out var targetSettlement));
            serverParty.SetMoveRaidSettlement(targetSettlement, MobileParty.NavigationType.Default, false);
            serverParty.Ai.Tick(dt);
            TestEnvironment.FlushCoalescer();
        });

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));

            client.Call(() =>
            {
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
    public void Party_SetMoveBesiegeSettlement_Sync()
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
            Assert.True(server.ObjectManager.TryGetObject<Settlement>(TargetSettlementId, out var targetSettlement));
            serverParty.SetMoveBesiegeSettlement(targetSettlement, MobileParty.NavigationType.Default);
            serverParty.Ai.Tick(dt);
            TestEnvironment.FlushCoalescer();
        });

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));

            client.Call(() =>
            {
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
    public void Party_SetMoveDefendSettlement_Sync()
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
            Assert.True(server.ObjectManager.TryGetObject<Settlement>(TargetSettlementId, out var targetSettlement));
            serverParty.SetMoveDefendSettlement(targetSettlement, false, MobileParty.NavigationType.Default);
            serverParty.Ai.Tick(dt);
            TestEnvironment.FlushCoalescer();
        });

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientParty));

            client.Call(() =>
            {
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
