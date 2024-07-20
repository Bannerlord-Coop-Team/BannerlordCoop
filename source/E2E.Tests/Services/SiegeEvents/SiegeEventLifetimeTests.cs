using E2E.Tests.Environment;
using E2E.Tests.Util;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using Xunit.Abstractions;

namespace E2E.Tests.Services.SiegeEvents;
public class SiegeEventLifetimeTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }

    private List<MethodBase> disabledMethods;

    public SiegeEventLifetimeTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        disabledMethods = new List<MethodBase>
        {
            AccessTools.Method(typeof(MobileParty), nameof(MobileParty.OnPartyJoinedSiegeInternal)),
        };

        disabledMethods.AddRange(AccessTools.GetDeclaredConstructors(typeof(SiegeEvent)));

    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerCreate_SiegeEvent_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        

        // Act
        string? beseigerCampId = null;
        server.Call(() =>
        {
            var beseigerCamp = GameObjectCreator.CreateInitializedObject<SiegeEvent>();

            Assert.True(server.ObjectManager.TryGetId(beseigerCamp, out beseigerCampId));
        },
        disabledMethods: disabledMethods);

        // Assert
        Assert.NotNull(beseigerCampId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<SiegeEvent>(beseigerCampId, out var _));
        }
    }

    [Fact]
    public void ClientCreate_SiegeEvent_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;

        string? settlementId = null;
        string? mobilePartyId = null;
        server.Call(() =>
        {
            var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            var mobileParty = GameObjectCreator.CreateInitializedObject<MobileParty>();

            Assert.True(server.ObjectManager.TryGetId(settlement, out settlementId));
            Assert.True(server.ObjectManager.TryGetId(mobileParty, out mobilePartyId));
        });

        Assert.NotNull(settlementId);
        Assert.NotNull(mobilePartyId);

        // Act
        string? clientBeseigerCampId = null;

        var firstClient = TestEnvironment.Clients.First();
        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.True(firstClient.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));

            var SiegeEvent = new SiegeEvent(settlement, mobileParty);

            Assert.False(firstClient.ObjectManager.TryGetId(SiegeEvent, out clientBeseigerCampId));
        },
        disabledMethods: disabledMethods);

        // Assert
        Assert.Null(clientBeseigerCampId);
    }
}
