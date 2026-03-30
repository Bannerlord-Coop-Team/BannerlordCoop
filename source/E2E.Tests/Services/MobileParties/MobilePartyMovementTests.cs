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
    private readonly string ServerId = "TestServer";

    private readonly string MobilePartyId = "TestParty";
    private readonly string TargetPartyId = "TargetParty";
    private readonly string TargetSettlementId = "TargetSettlement";

    public MobilePartyMovementTests(ITestOutputHelper output) : base(output)
    {
        TestEnvironment.Server.Container.Resolve<IControllerIdProvider>().SetControllerId(ServerId);



        MobilePartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        TargetPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        TargetSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();

        var controller = TestEnvironment.Server.Container.Resolve<IControlledEntityRegistry>();
        controller.RegisterAsControlled(ServerId, MobilePartyId);

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
        var dt = 0.1f;
        var point = new Vec2(0.1f, 0.2f);
        var campaignPoint = new CampaignVec2(point, true);

        var server = TestEnvironment.Server;

        // Act
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var serverParty));
            serverParty.SetMoveGoToPoint(campaignPoint, MobileParty.NavigationType.Default);
            serverParty.Ai.Tick(dt);
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
            serverParty.SetMoveGoToPoint(campaignPoint, MobileParty.NavigationType.Default);
            serverParty.Ai.Tick(dt);
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
            serverParty.SetMoveGoToPoint(campaignPoint, MobileParty.NavigationType.Default);
            serverParty.Ai.Tick(dt);
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
            serverParty.SetMoveGoToPoint(campaignPoint, MobileParty.NavigationType.Default);
            serverParty.Ai.Tick(dt);
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
            serverParty.ShortTermBehavior = AiBehavior.AssaultSettlement;
            serverParty.Ai.Tick(dt);
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
            serverParty.SetMoveGoToPoint(campaignPoint, MobileParty.NavigationType.Default);
            serverParty.Ai.Tick(dt);
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

            if (serverParty.Ai.AiBehaviorInteractable is not null)
            {
                Assert.True(client.ObjectManager.TryGetId(clientParty.Ai, out var aiId));
                Assert.True(client.ObjectManager.TryGetId((PartyBase)clientParty.Ai.AiBehaviorInteractable, out var targetSettlementId));
                Assert.Equal(TargetSettlementId, targetSettlementId);
            }

            if (serverParty.Ai.AiBehaviorPartyBase is not null)
            {
                Assert.True(client.ObjectManager.TryGetId(clientParty.Ai, out var aiId));
                Assert.True(client.ObjectManager.TryGetId(clientParty.Ai.AiBehaviorPartyBase, out var targetSettlementId));
                Assert.Equal(TargetSettlementId, targetSettlementId);
            }
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
            serverParty.SetMoveGoToPoint(campaignPoint, MobileParty.NavigationType.Default);
            serverParty.Ai.Tick(dt);
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
            serverParty.SetMoveGoToPoint(campaignPoint, MobileParty.NavigationType.Default);
            serverParty.Ai.Tick(dt);
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
            serverParty.SetMoveGoToPoint(campaignPoint, MobileParty.NavigationType.Default);
            serverParty.Ai.Tick(dt);
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
            serverParty.SetMoveGoToPoint(campaignPoint, MobileParty.NavigationType.Default);
            serverParty.Ai.Tick(dt);
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
            serverParty.SetMoveGoToPoint(campaignPoint, MobileParty.NavigationType.Default);
            serverParty.Ai.Tick(dt);
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
            serverParty.SetMoveGoToPoint(campaignPoint, MobileParty.NavigationType.Default);
            serverParty.Ai.Tick(dt);
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

    private void AssertPartyMovementValues(EnvironmentInstance client, MobileParty clientParty)

    {
        var server = TestEnvironment.Server;
        Assert.True(server.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var serverParty));

        Assert.Equal(serverParty.DefaultBehavior, clientParty.DefaultBehavior);
        Assert.Equal(serverParty.ShortTermBehavior, clientParty.ShortTermBehavior);
        Assert.Equal(serverParty.TargetPosition, clientParty.TargetPosition);
        Assert.Equal(serverParty.MoveTargetPoint, clientParty.MoveTargetPoint);
        Assert.Equal(serverParty.DesiredAiNavigationType, clientParty.DesiredAiNavigationType);

        if (serverParty.TargetParty is not null)
        {
            Assert.True(client.ObjectManager.TryGetId(clientParty.TargetParty, out var targetPartyId));
            Assert.Equal(TargetPartyId, targetPartyId);
        }

        if (serverParty.TargetSettlement is not null)
        {
            Assert.True(client.ObjectManager.TryGetId(clientParty.TargetSettlement, out var targetSettlementId));
            Assert.Equal(TargetSettlementId, targetSettlementId);
        }

        if (serverParty.Ai.AiBehaviorPartyBase is null)
        {
            Assert.Null(clientParty.Ai.AiBehaviorPartyBase);
        }

        if (serverParty.Ai.AiBehaviorInteractable is null)
        {
            Assert.Null(clientParty.Ai.AiBehaviorInteractable);
        }
    }
}
