using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.SettlementComponents;
public class SettlementComponentTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }

    EnvironmentInstance Server => TestEnvironment.Server;

    IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

    private readonly string SettlementComponentId;

    public SettlementComponentTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        SettlementComponentId = TestEnvironment.CreateRegisteredObject<Village>();
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }


    [Fact]
    public void Server_SettlementComponent_Sync()
    {
        // Arrange
        var server = TestEnvironment.Server;

        PartyBase partyBase = ObjectHelper.SkipConstructor<PartyBase>();
        Assert.True(server.ObjectManager.AddNewObject(partyBase, out var partyBaseId));

        // Create garrison instances on all clients
        foreach (var client in Clients)
        {
            var client_partyBase = ObjectHelper.SkipConstructor<PartyBase>();
            Assert.True(client.ObjectManager.AddExisting(partyBaseId, client_partyBase));
        }

        // Act
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<Village>(SettlementComponentId, out var settlementComponent));
            Assert.True(server.ObjectManager.TryGetObject<PartyBase>(partyBaseId, out var partyBase));

            Assert.Null(settlementComponent.Owner);

            settlementComponent.Owner = partyBase;
            settlementComponent.Gold = 5;
            settlementComponent.IsOwnerUnassigned = true;

            //Assert.Same(partyBase, settlementComponent.Owner);
            Assert.Equal(5, settlementComponent.Gold);
            Assert.True(settlementComponent.IsOwnerUnassigned);
        });

        // Assert
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Village>(SettlementComponentId, out var settlementComponent));

            Assert.True(client.ObjectManager.TryGetObject<PartyBase>(partyBaseId, out var clientPartyBase));

            //Assert.True(clientPartyBase == settlementComponent.Owner);
            Assert.Equal(5, settlementComponent.Gold);
            Assert.True(settlementComponent.IsOwnerUnassigned);
        }
    }
}

