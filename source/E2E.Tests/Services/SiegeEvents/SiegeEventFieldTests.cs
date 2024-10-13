using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using Xunit.Abstractions;

namespace E2E.Tests.Services.SiegeEvents;

public class SiegeEventFieldTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }

    private EnvironmentInstance Server => TestEnvironment.Server;

    private IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

    private readonly string SiegeEventId;

    public SiegeEventFieldTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        var SiegeEvent = ObjectHelper.SkipConstructor<SiegeEvent>();

        // Create SiegeEvent on the server
        Assert.True(Server.ObjectManager.AddNewObject(SiegeEvent, out SiegeEventId));

        // Create SiegeEvent on all clients
        foreach (var client in Clients)
        {
            var clientSiegeEvent = ObjectHelper.SkipConstructor<SiegeEvent>();
            Assert.True(client.ObjectManager.AddExisting(SiegeEventId, clientSiegeEvent));
        }
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerChangeBesiegedSettlement_SyncAllClients()
    {
        // Arrange
        var field = AccessTools.Field(typeof(SiegeEvent), nameof(SiegeEvent.BesiegedSettlement));
        var intercept = TestEnvironment.GetIntercept(field);
        Assert.True(Server.ObjectManager.AddNewObject(ObjectHelper.SkipConstructor<Settlement>(), out var settlementId));
        foreach (var client in Clients)
        {
            var clientSettlement = ObjectHelper.SkipConstructor<Settlement>();
            Assert.True(client.ObjectManager.AddExisting(settlementId, clientSettlement));
        }

        // Act
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<SiegeEvent>(SiegeEventId, out var serverSiegeEvent));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var serverSettlement));
            Assert.Null(serverSiegeEvent.BesiegedSettlement);

            /// Simulate the field changing
            intercept.Invoke(null, new object[] { serverSiegeEvent, serverSettlement });
            Assert.Same(serverSettlement, serverSiegeEvent.BesiegedSettlement);
        });

        // Assert
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<SiegeEvent>(SiegeEventId, out var clientSiegeEvent));

            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var clientSettlement));

            Assert.True(clientSettlement == clientSiegeEvent.BesiegedSettlement);
        }
    }

    [Fact]
    public void ServerChangeBesiegerCamp_SyncAllClients()
    {
        // Arrange
        var field = AccessTools.Field(typeof(SiegeEvent), nameof(SiegeEvent.BesiegerCamp));
        var intercept = TestEnvironment.GetIntercept(field);
        Assert.True(Server.ObjectManager.AddNewObject(ObjectHelper.SkipConstructor<BesiegerCamp>(), out var besiegerCampId));
        foreach (var client in Clients)
        {
            var clientBesiegerCamp = ObjectHelper.SkipConstructor<BesiegerCamp>();
            Assert.True(client.ObjectManager.AddExisting(besiegerCampId, clientBesiegerCamp));
        }

        // Act
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<SiegeEvent>(SiegeEventId, out var serverSiegeEvent));
            Assert.True(Server.ObjectManager.TryGetObject<BesiegerCamp>(besiegerCampId, out var serverBesiegerCamp));
            Assert.Null(serverSiegeEvent.BesiegerCamp);

            /// Simulate the field changing
            intercept.Invoke(null, new object[] { serverSiegeEvent, serverBesiegerCamp });
            Assert.Same(serverBesiegerCamp, serverSiegeEvent.BesiegerCamp);
        });

        // Assert
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<SiegeEvent>(SiegeEventId, out var clientSiegeEvent));

            Assert.True(client.ObjectManager.TryGetObject<BesiegerCamp>(besiegerCampId, out var clientBesiegerCamp));

            Assert.True(clientBesiegerCamp == clientSiegeEvent.BesiegerCamp);
        }
    }

    [Fact]
    public void ServerChange_isBesiegerDefeated_SyncAllClients()
    {
        // Arrange
        var field = AccessTools.Field(typeof(SiegeEvent), nameof(SiegeEvent._isBesiegerDefeated));
        var intercept = TestEnvironment.GetIntercept(field);

        // Act
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<SiegeEvent>(SiegeEventId, out var serverSiegeEvent));
            Assert.False(serverSiegeEvent._isBesiegerDefeated);

            /// Simulate the field changing
            intercept.Invoke(null, new object[] { serverSiegeEvent, true });

            Assert.True(serverSiegeEvent._isBesiegerDefeated);
        });

        // Assert
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<SiegeEvent>(SiegeEventId, out var clientSiegeEvent));
            Assert.True(clientSiegeEvent._isBesiegerDefeated);
        }
    }


}