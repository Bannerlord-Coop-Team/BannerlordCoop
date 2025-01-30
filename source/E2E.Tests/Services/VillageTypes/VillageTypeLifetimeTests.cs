using System.Reflection;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using Xunit.Abstractions;

namespace E2E.Tests.Services.VillageTypes;

public class VillageTypeLifetimeTests : IDisposable
{
    private readonly List<MethodBase> disabledMethods;
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;
    private IEnumerable<EnvironmentInstance> AllEnvironmentInstances => Clients.Append(Server);

    public VillageTypeLifetimeTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerCreateSiegeEvent_SyncAllClients()
    {
        // Arrange
        string? villageTypeId = null;

        // Act
        Server.Call(() =>
        {
            VillageType villageType = new VillageType("test");
            Assert.True(Server.ObjectManager.TryGetId(villageType, out villageTypeId));
        }
        );

        // Assert
        Assert.NotNull(villageTypeId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<VillageType>(villageTypeId, out var _));
        }
    }

    [Fact]
    public void ClientCreateSiegeEvent_DoesNothing()
    {
        // Arrange
        string? clientSiegeEventId = null;

        // Act
        var firstClient = TestEnvironment.Clients.First();
        firstClient.Call(() =>
        {
            VillageType villageType = new VillageType("test");

            Assert.False(firstClient.ObjectManager.TryGetId(villageType, out clientSiegeEventId));
        });

        // Assert
        Assert.Null(clientSiegeEventId);
    }
}

