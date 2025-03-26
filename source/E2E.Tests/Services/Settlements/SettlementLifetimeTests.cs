using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Settlements;

public class SettlementLifetimeTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;

    public SettlementLifetimeTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerCreateSettlement_SyncAllClients()
    {
        // Arrange
        string? settlementId = null;

        // Act
        Server.Call(() =>
        {
            var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            Assert.True(Server.ObjectManager.TryGetId(settlement, out settlementId));
        });

        // Assert
        Assert.NotNull(settlementId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var _));
        }
    }

    [Fact]
    public void ClientCreateSettlement_DoesNothing()
    {
        // Arrange
        string? clientSettlementId = null;

        // Act
        var firstClient = TestEnvironment.Clients.First();
        firstClient.Call(() =>
        {
            var Settlement = ObjectHelper.SkipConstructor<Settlement>();

            Assert.False(firstClient.ObjectManager.TryGetId(Settlement, out clientSettlementId));
        });

        // Assert
        Assert.Null(clientSettlementId);
    }
}

