using TaleWorlds.CampaignSystem;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using HarmonyLib;
using Common.Util;
using System.Reflection;
using Xunit.Abstractions;
using static Common.Extensions.ReflectionExtensions;

namespace E2E.Tests.Services.Kingdoms;

public class KingdomLifetimeTests : IDisposable
{
    private readonly List<MethodBase> disabledMethods;
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;
    private IEnumerable<EnvironmentInstance> AllEnvironmentInstances => Clients.Append(Server);

    public KingdomLifetimeTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        disabledMethods = new List<MethodBase> {
            //Add your disabled methods
        };
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

        [Fact]
    public void ServerCreateKingdom_SyncAllClients()
    {
        // Arrange
        string? kingdomId = null;

        // Act
        Server.Call(() =>
        {
            var kingdom = GameObjectCreator.CreateInitializedObject<Kingdom>();
            Assert.True(Server.ObjectManager.TryGetId(kingdom, out kingdomId));
        }, disabledMethods
        );

        // Assert
        Assert.NotNull(kingdomId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var _));
        }
    }

    [Fact]
    public void ClientCreateKingdom_DoesNothing()
    {
        // Arrange
        string? clientKingdomId = null;

        // Act
        var firstClient = TestEnvironment.Clients.First();
        firstClient.Call(() =>
        {
            var Kingdom = ObjectHelper.SkipConstructor<Kingdom>();

            Assert.False(firstClient.ObjectManager.TryGetId(Kingdom, out clientKingdomId));
        });

        // Assert
        Assert.Null(clientKingdomId);
    }
}

    