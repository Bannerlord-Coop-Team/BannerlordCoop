using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using Xunit.Abstractions;

namespace E2E.Tests.Services.SiegeStrategies;

public class SiegeStrategyLifetimeTests : IDisposable
{
    private readonly List<MethodBase> disabledMethods;
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;
    private IEnumerable<EnvironmentInstance> AllEnvironmentInstances => Clients.Append(Server);

    public SiegeStrategyLifetimeTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        disabledMethods = new List<MethodBase>
        {
            AccessTools.Method(typeof(MobileParty), nameof(MobileParty.OnPartyJoinedSiegeInternal)),
        };

        disabledMethods.AddRange(AccessTools.GetDeclaredConstructors(typeof(SiegeStrategy)));
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerCreateSiegeStrategy_SyncAllClients()
    {
        // Arrange
        string? SiegeStrategyId = null;

        // Act
        Server.Call(() =>
        {
            var siegeStrategy = GameObjectCreator.CreateInitializedObject<SiegeStrategy>();
            Assert.True(Server.ObjectManager.TryGetId(siegeStrategy, out SiegeStrategyId));
        });

        // Assert
        Assert.NotNull(SiegeStrategyId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<SiegeStrategy>(SiegeStrategyId, out var _));
        }
    }

    [Fact]
    public void ClientCreateSiegeStrategy_DoesNothing()
    {
        // Arrange
        string? clientSiegeStrategyId = null;

        // Act
        var firstClient = TestEnvironment.Clients.First();
        firstClient.Call(() =>
        {
            var SiegeStrategy = ObjectHelper.SkipConstructor<SiegeStrategy>();

            Assert.False(firstClient.ObjectManager.TryGetId(SiegeStrategy, out clientSiegeStrategyId));
        });

        // Assert
        Assert.Null(clientSiegeStrategyId);
    }
}

