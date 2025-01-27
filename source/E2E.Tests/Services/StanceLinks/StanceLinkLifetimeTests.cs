using TaleWorlds.CampaignSystem;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using HarmonyLib;
using Common.Util;
using System.Reflection;
using Xunit.Abstractions;
using static Common.Extensions.ReflectionExtensions;

namespace E2E.Tests.Services.StanceLinks;

public class StanceLinkLifetimeTests : IDisposable
{
    private readonly List<MethodBase> disabledMethods;
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;
    private IEnumerable<EnvironmentInstance> AllEnvironmentInstances => Clients.Append(Server);

    public StanceLinkLifetimeTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        disabledMethods = new List<MethodBase>
        {
            //Add your disabled methods
        };
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerCreateStanceLink_SyncAllClients()
    {
        // Arrange
        string? stanceLinkId = null;

        // Act
        Server.Call(() =>
        {
            var stanceLink = GameObjectCreator.CreateInitializedObject<StanceLink>();
            Assert.True(Server.ObjectManager.TryGetId(stanceLink, out stanceLinkId));
        }, disabledMethods
        );

        // Assert
        Assert.NotNull(stanceLinkId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<StanceLink>(stanceLinkId, out var _));
        }
    }

    [Fact]
    public void ClientCreateStanceLink_DoesNothing()
    {
        // Arrange
        string? clientStanceLinkId = null;

        // Act
        var firstClient = TestEnvironment.Clients.First();
        firstClient.Call(() =>
        {
            var StanceLink = ObjectHelper.SkipConstructor<StanceLink>();

            Assert.False(firstClient.ObjectManager.TryGetId(StanceLink, out clientStanceLinkId));
        });

        // Assert
        Assert.Null(clientStanceLinkId);
    }
}
