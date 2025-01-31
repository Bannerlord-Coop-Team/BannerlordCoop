using E2E.Tests.Environment;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Alleys;

public class AlleyCreationTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }
    public AlleyCreationTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerCreateAlley_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;
        string alleyId = null;

        // Act
        server.Call(() =>
        {
            var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            var alley = new Alley(settlement, "testAlley", new TaleWorlds.Localization.TextObject("testTextObject"));

            Assert.True(server.ObjectManager.TryGetId(alley, out alleyId));

            // _owner field sync test
            Assert.Null(alley.Owner);
            var hero = GameObjectCreator.CreateInitializedObject<Hero>();
            alley.SetOwner(hero);
            Assert.Equal(hero, alley.Owner);
        });

        // Assert
        Assert.NotNull(alleyId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Alley>(alleyId, out var alley));
            Assert.NotNull(alley.Owner);
        }
    }

    [Fact]
    public void ClientCreateAlley_DoesNothing()
    {
        // Arrange
        var client1 = TestEnvironment.Clients.First();

        // Act
        string? clientAlleyId = null;
        client1.Call(() =>
        {
            var alley = new Alley(GameObjectCreator.CreateInitializedObject<Settlement>(), "testClientAlley", new TaleWorlds.Localization.TextObject("testTextObject"));
            Assert.False(client1.ObjectManager.TryGetId(alley, out clientAlleyId));
        });

        // Assert
        Assert.Null(clientAlleyId);
    }

    [Fact]
    public void ClientSetOwner_DoesNothing()
    {
        // Arrange
        var client1 = TestEnvironment.Clients.First();
        var server = TestEnvironment.Server;
        string alleyId = null;
        string heroId = null;
        server.Call(() =>
        {
            alleyId = TestEnvironment.CreateRegisteredObject<Alley>();
            heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        });
            // Act
            client1.Call(() =>
        {
            Assert.True(client1.ObjectManager.TryGetObject<Alley>(alleyId, out var alley));
            Assert.True(client1.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            alley.SetOwner(hero);
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Alley>(alleyId, out var alley));
            Assert.Null(alley.Owner);
        }
    }
}