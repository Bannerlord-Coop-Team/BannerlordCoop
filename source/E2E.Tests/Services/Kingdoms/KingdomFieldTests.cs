using TaleWorlds.CampaignSystem;
using System;
using TaleWorlds.CampaignSystem.Settlements;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using HarmonyLib;
using System.Reflection;
using Xunit.Abstractions;
using static Common.Extensions.ReflectionExtensions;
using Common.Util;

namespace E2E.Tests.Services.Kingdoms;

public class KingdomFieldTests : IDisposable
{
    private readonly List<MethodBase> disabledMethods;
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;
    private IEnumerable<EnvironmentInstance> AllEnvironmentInstances => Clients.Append(Server);

    private readonly string kingdomId;

    public KingdomFieldTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        disabledMethods = new List<MethodBase> {
            //Add your disabled methods
        };

        // Create Kingdom on the server
        kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>(disabledMethods);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }


    [Fact]
    public void ServerChangeKingdomKingdomMidSettlement_SyncAllClients()
    {
        // Arrange
        var field = AccessTools.Field(typeof(Kingdom), nameof(Kingdom._kingdomMidSettlement));
        var intercept = TestEnvironment.GetIntercept(field);

        /// Create instances on server
        Assert.True(Server.ObjectManager.AddNewObject(ObjectHelper.SkipConstructor<Settlement>(), out var kingdomMidSettlementId));

        /// Create instances on all clients
        foreach (var client in Clients)
        {
            var clientKingdomMidSettlement = ObjectHelper.SkipConstructor<Settlement>();
            Assert.True(client.ObjectManager.AddExisting(kingdomMidSettlementId, clientKingdomMidSettlement));
        }

        // Act
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var Kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(kingdomMidSettlementId, out var serverKingdomMidSettlement));

            Assert.Null(Kingdom._kingdomMidSettlement);

            /// Simulate the field changing
            intercept.Invoke(null, new object[] { Kingdom, serverKingdomMidSettlement});

            Assert.Same(serverKingdomMidSettlement, Kingdom._kingdomMidSettlement);
        });

        // Assert
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var Kingdom));

            Assert.True(client.ObjectManager.TryGetObject<Settlement>(kingdomMidSettlementId, out var clientKingdomMidSettlement));

            Assert.True(clientKingdomMidSettlement == Kingdom._kingdomMidSettlement);
        }
    }
    
    [Fact]
    public void ServerChangeKingdomRulingClan_SyncAllClients()
    {
        // Arrange
        var field = AccessTools.Field(typeof(Kingdom), nameof(Kingdom._rulingClan));
        var intercept = TestEnvironment.GetIntercept(field);

        /// Create instances on server
        Assert.True(Server.ObjectManager.AddNewObject(ObjectHelper.SkipConstructor<Clan>(), out var rulingClanId));

        /// Create instances on all clients
        foreach (var client in Clients)
        {
            var clientRulingClan = ObjectHelper.SkipConstructor<Clan>();
            Assert.True(client.ObjectManager.AddExisting(rulingClanId, clientRulingClan));
        }

        // Act
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var Kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(rulingClanId, out var serverRulingClan));

            Assert.Null(Kingdom._rulingClan);

            /// Simulate the field changing
            intercept.Invoke(null, new object[] { Kingdom, serverRulingClan});

            Assert.Same(serverRulingClan, Kingdom._rulingClan);
        });

        // Assert
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var Kingdom));

            Assert.True(client.ObjectManager.TryGetObject<Clan>(rulingClanId, out var clientRulingClan));

            Assert.True(clientRulingClan == Kingdom._rulingClan);
        }
    }
    

    [Fact]
    public void ServerChangeKingdomPoliticalStagnation_SyncAllClients()
    {
        // Arrange
        var field = AccessTools.Field(typeof(Kingdom), nameof(Kingdom.PoliticalStagnation));
        var intercept = TestEnvironment.GetIntercept(field);
        Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var serverKingdom));
        var newValue=Random<Int32>();

        // Act
        Server.Call(() =>
        {
            /// Simulate the field changing
            intercept.Invoke(null, new object[] { serverKingdom, newValue });

            Assert.Equal(newValue, serverKingdom.PoliticalStagnation);
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var clientKingdom));
            Assert.Equal(serverKingdom.PoliticalStagnation, clientKingdom.PoliticalStagnation);
        }
    }  
    
    [Fact]
    public void ServerChangeKingdomAggressiveness_SyncAllClients()
    {
        // Arrange
        var field = AccessTools.Field(typeof(Kingdom), nameof(Kingdom._aggressiveness));
        var intercept = TestEnvironment.GetIntercept(field);
        Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var serverKingdom));
        var newValue=Random<Single>();

        // Act
        Server.Call(() =>
        {
            /// Simulate the field changing
            intercept.Invoke(null, new object[] { serverKingdom, newValue });

            Assert.Equal(newValue, serverKingdom._aggressiveness);
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var clientKingdom));
            Assert.Equal(serverKingdom._aggressiveness, clientKingdom._aggressiveness);
        }
    }  
    
    [Fact]
    public void ServerChangeKingdomIsEliminated_SyncAllClients()
    {
        // Arrange
        var field = AccessTools.Field(typeof(Kingdom), nameof(Kingdom._isEliminated));
        var intercept = TestEnvironment.GetIntercept(field);
        Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var serverKingdom));
        var newValue=Random<Boolean>();

        // Act
        Server.Call(() =>
        {
            /// Simulate the field changing
            intercept.Invoke(null, new object[] { serverKingdom, newValue });

            Assert.Equal(newValue, serverKingdom._isEliminated);
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var clientKingdom));
            Assert.Equal(serverKingdom._isEliminated, clientKingdom._isEliminated);
        }
    }  
    
    [Fact]
    public void ServerChangeKingdomKingdomBudgetWallet_SyncAllClients()
    {
        // Arrange
        var field = AccessTools.Field(typeof(Kingdom), nameof(Kingdom._kingdomBudgetWallet));
        var intercept = TestEnvironment.GetIntercept(field);
        Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var serverKingdom));
        var newValue=Random<Int32>();

        // Act
        Server.Call(() =>
        {
            /// Simulate the field changing
            intercept.Invoke(null, new object[] { serverKingdom, newValue });

            Assert.Equal(newValue, serverKingdom._kingdomBudgetWallet);
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var clientKingdom));
            Assert.Equal(serverKingdom._kingdomBudgetWallet, clientKingdom._kingdomBudgetWallet);
        }
    }  
    
    [Fact]
    public void ServerChangeKingdomTributeWallet_SyncAllClients()
    {
        // Arrange
        var field = AccessTools.Field(typeof(Kingdom), nameof(Kingdom._tributeWallet));
        var intercept = TestEnvironment.GetIntercept(field);
        Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var serverKingdom));
        var newValue=Random<Int32>();

        // Act
        Server.Call(() =>
        {
            /// Simulate the field changing
            intercept.Invoke(null, new object[] { serverKingdom, newValue });

            Assert.Equal(newValue, serverKingdom._tributeWallet);
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var clientKingdom));
            Assert.Equal(serverKingdom._tributeWallet, clientKingdom._tributeWallet);
        }
    }  
    
    [Fact]
    public void ServerChangeKingdomDistanceToClosestNonAllyFortificationCacheDirty_SyncAllClients()
    {
        // Arrange
        var field = AccessTools.Field(typeof(Kingdom), nameof(Kingdom._distanceToClosestNonAllyFortificationCacheDirty));
        var intercept = TestEnvironment.GetIntercept(field);
        Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var serverKingdom));
        var newValue=Random<Boolean>();

        // Act
        Server.Call(() =>
        {
            /// Simulate the field changing
            intercept.Invoke(null, new object[] { serverKingdom, newValue });

            Assert.Equal(newValue, serverKingdom._distanceToClosestNonAllyFortificationCacheDirty);
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var clientKingdom));
            Assert.Equal(serverKingdom._distanceToClosestNonAllyFortificationCacheDirty, clientKingdom._distanceToClosestNonAllyFortificationCacheDirty);
        }
    }  
    }

    