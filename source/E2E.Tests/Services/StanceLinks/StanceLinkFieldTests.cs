using TaleWorlds.CampaignSystem;
using System;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using HarmonyLib;
using System.Reflection;
using Xunit.Abstractions;
using static Common.Extensions.ReflectionExtensions;
using Common.Util;

namespace E2E.Tests.Services.StanceLinks;

public class StanceLinkFieldTests : IDisposable
{
    private readonly List<MethodBase> disabledMethods;
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;
    private IEnumerable<EnvironmentInstance> AllEnvironmentInstances => Clients.Append(Server);

    private readonly string stanceLinkId;

    public StanceLinkFieldTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        disabledMethods = new List<MethodBase>
        {
            //Add your disabled methods
        };

        // Create StanceLink on the server
        stanceLinkId = TestEnvironment.CreateRegisteredObject<StanceLink>(disabledMethods);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerChangeStanceLinkBehaviorPriority_SyncAllClients()
    {
        // Arrange
        var field = AccessTools.Field(typeof(StanceLink), nameof(StanceLink.BehaviorPriority));
        var intercept = TestEnvironment.GetIntercept(field);
        Assert.True(Server.ObjectManager.TryGetObject<StanceLink>(stanceLinkId, out var serverStanceLink));
        var newValue = Random<Int32>();

        // Act
        Server.Call(() =>
        {
            /// Simulate the field changing
            intercept.Invoke(null, new object[] { serverStanceLink, newValue });

            Assert.Equal(newValue, serverStanceLink.BehaviorPriority);
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<StanceLink>(stanceLinkId, out var clientStanceLink));
            Assert.Equal(serverStanceLink.BehaviorPriority, clientStanceLink.BehaviorPriority);
        }
    }

    [Fact]
    public void ServerChangeStanceLinkCasualties1_SyncAllClients()
    {
        // Arrange
        var field = AccessTools.Field(typeof(StanceLink), nameof(StanceLink._casualties1));
        var intercept = TestEnvironment.GetIntercept(field);
        Assert.True(Server.ObjectManager.TryGetObject<StanceLink>(stanceLinkId, out var serverStanceLink));
        var newValue = Random<Int32>();

        // Act
        Server.Call(() =>
        {
            /// Simulate the field changing
            intercept.Invoke(null, new object[] { serverStanceLink, newValue });

            Assert.Equal(newValue, serverStanceLink._casualties1);
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<StanceLink>(stanceLinkId, out var clientStanceLink));
            Assert.Equal(serverStanceLink._casualties1, clientStanceLink._casualties1);
        }
    }

    [Fact]
    public void ServerChangeStanceLinkCasualties2_SyncAllClients()
    {
        // Arrange
        var field = AccessTools.Field(typeof(StanceLink), nameof(StanceLink._casualties2));
        var intercept = TestEnvironment.GetIntercept(field);
        Assert.True(Server.ObjectManager.TryGetObject<StanceLink>(stanceLinkId, out var serverStanceLink));
        var newValue = Random<Int32>();

        // Act
        Server.Call(() =>
        {
            /// Simulate the field changing
            intercept.Invoke(null, new object[] { serverStanceLink, newValue });

            Assert.Equal(newValue, serverStanceLink._casualties2);
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<StanceLink>(stanceLinkId, out var clientStanceLink));
            Assert.Equal(serverStanceLink._casualties2, clientStanceLink._casualties2);
        }
    }

    [Fact]
    public void ServerChangeStanceLinkDailyTributeFrom1To2_SyncAllClients()
    {
        // Arrange
        var field = AccessTools.Field(typeof(StanceLink), nameof(StanceLink._dailyTributeFrom1To2));
        var intercept = TestEnvironment.GetIntercept(field);
        Assert.True(Server.ObjectManager.TryGetObject<StanceLink>(stanceLinkId, out var serverStanceLink));
        var newValue = Random<Int32>();

        // Act
        Server.Call(() =>
        {
            /// Simulate the field changing
            intercept.Invoke(null, new object[] { serverStanceLink, newValue });

            Assert.Equal(newValue, serverStanceLink._dailyTributeFrom1To2);
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<StanceLink>(stanceLinkId, out var clientStanceLink));
            Assert.Equal(serverStanceLink._dailyTributeFrom1To2, clientStanceLink._dailyTributeFrom1To2);
        }
    }

    [Fact]
    public void ServerChangeStanceLinkIsAtConstantWar_SyncAllClients()
    {
        // Arrange
        var field = AccessTools.Field(typeof(StanceLink), nameof(StanceLink._isAtConstantWar));
        var intercept = TestEnvironment.GetIntercept(field);
        Assert.True(Server.ObjectManager.TryGetObject<StanceLink>(stanceLinkId, out var serverStanceLink));
        var newValue = Random<Boolean>();

        // Act
        Server.Call(() =>
        {
            /// Simulate the field changing
            intercept.Invoke(null, new object[] { serverStanceLink, newValue });

            Assert.Equal(newValue, serverStanceLink._isAtConstantWar);
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<StanceLink>(stanceLinkId, out var clientStanceLink));
            Assert.Equal(serverStanceLink._isAtConstantWar, clientStanceLink._isAtConstantWar);
        }
    }

    [Fact]
    public void ServerChangeStanceLinkPeaceDeclarationDate_SyncAllClients()
    {
        // Arrange
        var field = AccessTools.Field(typeof(StanceLink), nameof(StanceLink._peaceDeclarationDate));
        var intercept = TestEnvironment.GetIntercept(field);
        Assert.True(Server.ObjectManager.TryGetObject<StanceLink>(stanceLinkId, out var serverStanceLink));
        var newValue = Random<CampaignTime>();

        // Act
        Server.Call(() =>
        {
            /// Simulate the field changing
            intercept.Invoke(null, new object[] { serverStanceLink, newValue });

            Assert.Equal(newValue, serverStanceLink._peaceDeclarationDate);
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<StanceLink>(stanceLinkId, out var clientStanceLink));
            Assert.Equal(serverStanceLink._peaceDeclarationDate, clientStanceLink._peaceDeclarationDate);
        }
    }

    [Fact]
    public void ServerChangeStanceLinkStanceType_SyncAllClients()
    {
        // Arrange
        var field = AccessTools.Field(typeof(StanceLink), nameof(StanceLink._stanceType));
        var intercept = TestEnvironment.GetIntercept(field);
        Assert.True(Server.ObjectManager.TryGetObject<StanceLink>(stanceLinkId, out var serverStanceLink));
        var newValue = Random<StanceType>();

        // Act
        Server.Call(() =>
        {
            /// Simulate the field changing
            intercept.Invoke(null, new object[] { serverStanceLink, newValue });

            Assert.Equal(newValue, serverStanceLink._stanceType);
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<StanceLink>(stanceLinkId, out var clientStanceLink));
            Assert.Equal(serverStanceLink._stanceType, clientStanceLink._stanceType);
        }
    }

    [Fact]
    public void ServerChangeStanceLinkSuccessfulRaids1_SyncAllClients()
    {
        // Arrange
        var field = AccessTools.Field(typeof(StanceLink), nameof(StanceLink._successfulRaids1));
        var intercept = TestEnvironment.GetIntercept(field);
        Assert.True(Server.ObjectManager.TryGetObject<StanceLink>(stanceLinkId, out var serverStanceLink));
        var newValue = Random<Int32>();

        // Act
        Server.Call(() =>
        {
            /// Simulate the field changing
            intercept.Invoke(null, new object[] { serverStanceLink, newValue });

            Assert.Equal(newValue, serverStanceLink._successfulRaids1);
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<StanceLink>(stanceLinkId, out var clientStanceLink));
            Assert.Equal(serverStanceLink._successfulRaids1, clientStanceLink._successfulRaids1);
        }
    }

    [Fact]
    public void ServerChangeStanceLinkSuccessfulRaids2_SyncAllClients()
    {
        // Arrange
        var field = AccessTools.Field(typeof(StanceLink), nameof(StanceLink._successfulRaids2));
        var intercept = TestEnvironment.GetIntercept(field);
        Assert.True(Server.ObjectManager.TryGetObject<StanceLink>(stanceLinkId, out var serverStanceLink));
        var newValue = Random<Int32>();

        // Act
        Server.Call(() =>
        {
            /// Simulate the field changing
            intercept.Invoke(null, new object[] { serverStanceLink, newValue });

            Assert.Equal(newValue, serverStanceLink._successfulRaids2);
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<StanceLink>(stanceLinkId, out var clientStanceLink));
            Assert.Equal(serverStanceLink._successfulRaids2, clientStanceLink._successfulRaids2);
        }
    }

    [Fact]
    public void ServerChangeStanceLinkSuccessfulSieges1_SyncAllClients()
    {
        // Arrange
        var field = AccessTools.Field(typeof(StanceLink), nameof(StanceLink._successfulSieges1));
        var intercept = TestEnvironment.GetIntercept(field);
        Assert.True(Server.ObjectManager.TryGetObject<StanceLink>(stanceLinkId, out var serverStanceLink));
        var newValue = Random<Int32>();

        // Act
        Server.Call(() =>
        {
            /// Simulate the field changing
            intercept.Invoke(null, new object[] { serverStanceLink, newValue });

            Assert.Equal(newValue, serverStanceLink._successfulSieges1);
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<StanceLink>(stanceLinkId, out var clientStanceLink));
            Assert.Equal(serverStanceLink._successfulSieges1, clientStanceLink._successfulSieges1);
        }
    }

    [Fact]
    public void ServerChangeStanceLinkSuccessfulSieges2_SyncAllClients()
    {
        // Arrange
        var field = AccessTools.Field(typeof(StanceLink), nameof(StanceLink._successfulSieges2));
        var intercept = TestEnvironment.GetIntercept(field);
        Assert.True(Server.ObjectManager.TryGetObject<StanceLink>(stanceLinkId, out var serverStanceLink));
        var newValue = Random<Int32>();

        // Act
        Server.Call(() =>
        {
            /// Simulate the field changing
            intercept.Invoke(null, new object[] { serverStanceLink, newValue });

            Assert.Equal(newValue, serverStanceLink._successfulSieges2);
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<StanceLink>(stanceLinkId, out var clientStanceLink));
            Assert.Equal(serverStanceLink._successfulSieges2, clientStanceLink._successfulSieges2);
        }
    }

    [Fact]
    public void ServerChangeStanceLinkTotalTributePaidby1_SyncAllClients()
    {
        // Arrange
        var field = AccessTools.Field(typeof(StanceLink), nameof(StanceLink._totalTributePaidby1));
        var intercept = TestEnvironment.GetIntercept(field);
        Assert.True(Server.ObjectManager.TryGetObject<StanceLink>(stanceLinkId, out var serverStanceLink));
        var newValue = Random<Int32>();

        // Act
        Server.Call(() =>
        {
            /// Simulate the field changing
            intercept.Invoke(null, new object[] { serverStanceLink, newValue });

            Assert.Equal(newValue, serverStanceLink._totalTributePaidby1);
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<StanceLink>(stanceLinkId, out var clientStanceLink));
            Assert.Equal(serverStanceLink._totalTributePaidby1, clientStanceLink._totalTributePaidby1);
        }
    }

    [Fact]
    public void ServerChangeStanceLinkTotalTributePaidby2_SyncAllClients()
    {
        // Arrange
        var field = AccessTools.Field(typeof(StanceLink), nameof(StanceLink._totalTributePaidby2));
        var intercept = TestEnvironment.GetIntercept(field);
        Assert.True(Server.ObjectManager.TryGetObject<StanceLink>(stanceLinkId, out var serverStanceLink));
        var newValue = Random<Int32>();

        // Act
        Server.Call(() =>
        {
            /// Simulate the field changing
            intercept.Invoke(null, new object[] { serverStanceLink, newValue });

            Assert.Equal(newValue, serverStanceLink._totalTributePaidby2);
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<StanceLink>(stanceLinkId, out var clientStanceLink));
            Assert.Equal(serverStanceLink._totalTributePaidby2, clientStanceLink._totalTributePaidby2);
        }
    }

    [Fact]
    public void ServerChangeStanceLinkWarStartDate_SyncAllClients()
    {
        // Arrange
        var field = AccessTools.Field(typeof(StanceLink), nameof(StanceLink._warStartDate));
        var intercept = TestEnvironment.GetIntercept(field);
        Assert.True(Server.ObjectManager.TryGetObject<StanceLink>(stanceLinkId, out var serverStanceLink));
        var newValue = Random<CampaignTime>();

        // Act
        Server.Call(() =>
        {
            /// Simulate the field changing
            intercept.Invoke(null, new object[] { serverStanceLink, newValue });

            Assert.Equal(newValue, serverStanceLink._warStartDate);
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<StanceLink>(stanceLinkId, out var clientStanceLink));
            Assert.Equal(serverStanceLink._warStartDate, clientStanceLink._warStartDate);
        }
    }
}