using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using Xunit.Abstractions;

namespace E2E.Tests.Services.BesiegerCamps;

public class BesiegerCampLifetimeTests : IDisposable
{
    private readonly List<MethodBase> disabledMethods;
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

    public BesiegerCampLifetimeTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        disabledMethods = new List<MethodBase> {
                AccessTools.Method(typeof(MobileParty), nameof(MobileParty.OnPartyJoinedSiegeInternal)),
                AccessTools.Method(typeof(BesiegerCamp), nameof(BesiegerCamp.SetSiegeCampPartyPosition)),
                AccessTools.Method(typeof(BesiegerCamp), nameof(BesiegerCamp.InitializeSiegeEventSide))
        };
        disabledMethods.AddRange(AccessTools.GetDeclaredConstructors(typeof(SiegeEvent)));
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerCreate_BesiegerCamp_SyncAllClients()
    {
        // Arrange
        string? beseigerCampId = null;

        // Act
        Server.Call(() =>
        {
            var beseigerCamp = GameObjectCreator.CreateInitializedObject<BesiegerCamp>();
            Assert.True(Server.ObjectManager.TryGetId(beseigerCamp, out beseigerCampId));
        }, disabledMethods
        );

        // Assert
        Assert.NotNull(beseigerCampId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<BesiegerCamp>(beseigerCampId, out var _));
        }
    }

    [Fact]
    public void ClientCreate_BesiegerCamp_DoesNothing()
    {
        // Arrange
        string? mapEventId = null;
        string? mobilePartyId = null;
        Server.Call(() =>
        {
            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
            var mobileParty = GameObjectCreator.CreateInitializedObject<MobileParty>();

            Assert.True(Server.ObjectManager.TryGetId(mapEvent, out mapEventId));
            Assert.True(Server.ObjectManager.TryGetId(mobileParty, out mobilePartyId));
        });

        Assert.NotNull(mapEventId);
        Assert.NotNull(mobilePartyId);

        // Act
        string? clientBeseigerCampId = null;

        var firstClient = TestEnvironment.Clients.First();
        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(firstClient.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));

            var BesiegerCamp = new BesiegerCamp(null);

            Assert.False(firstClient.ObjectManager.TryGetId(BesiegerCamp, out clientBeseigerCampId));
        });

        // Assert
        Assert.Null(clientBeseigerCampId);
    }
}