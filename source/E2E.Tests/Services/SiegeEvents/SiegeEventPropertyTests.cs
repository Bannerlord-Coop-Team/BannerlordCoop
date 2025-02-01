using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.CampaignSystem;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using HarmonyLib;
using System.Reflection;
using Xunit.Abstractions;
using Common.Util;
using static Common.Extensions.ReflectionExtensions;
using TaleWorlds.CampaignSystem.Party;

namespace E2E.Tests.Services.SiegeEvents;

public class SiegeEventPropertyTests : IDisposable
{
    private readonly List<MethodBase> disabledMethods;
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;
    private IEnumerable<EnvironmentInstance> AllEnvironmentInstances => Clients.Append(Server);

    private readonly string siegeEventId;

    public SiegeEventPropertyTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        disabledMethods = new List<MethodBase>
        {
            AccessTools.Method(typeof(MobileParty), nameof(MobileParty.OnPartyJoinedSiegeInternal)),
        };

        disabledMethods.AddRange(AccessTools.GetDeclaredConstructors(typeof(SiegeEvent)));

        // Create SiegeEvent on the server
        siegeEventId = TestEnvironment.CreateRegisteredObject<SiegeEvent>(disabledMethods);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }


    [Fact]
    public void ServerChangeSiegeEventSiegeStartTime_SyncAllClients()
    {
        // Arrange
        Assert.True(Server.ObjectManager.TryGetObject<SiegeEvent>(siegeEventId, out var serverSiegeEvent));
        var newValue=Random<CampaignTime>();

        // Act
        Server.Call(() =>
        {
            serverSiegeEvent.SiegeStartTime = newValue;
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<SiegeEvent>(siegeEventId, out var clientSiegeEvent));
            Assert.Equal(serverSiegeEvent.SiegeStartTime, clientSiegeEvent.SiegeStartTime);
        }
    }
    
}
