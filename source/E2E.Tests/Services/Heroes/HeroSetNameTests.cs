using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Heroes;

public class HeroSetNameTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }

    IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

    public HeroSetNameTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerSetName_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        string HeroId = null;

        // Create new text objects for name fields for server to set
        var originalFullName = new TextObject("Test Name");
        var originalFirstName = new TextObject("Name");

        // Act
        server.Call(() =>
        {
            Hero hero = GameObjectCreator.CreateInitializedObject<Hero>();
            hero.SetName(originalFullName, originalFirstName);

            server.ObjectManager.TryGetId(hero, out HeroId);

            // Assert
            Assert.Equal(originalFullName.Value, hero.Name.Value);
            Assert.Equal(originalFirstName.Value, hero.FirstName.Value);
        });

        foreach(var client in Clients)
        {
            client.ObjectManager.TryGetObject(HeroId, out Hero hero);

            Assert.Equal(originalFullName.Value, hero.Name.Value);
            Assert.Equal(originalFirstName.Value, hero.FirstName.Value);
        }
    }

    [Fact]
    public void ClientSetName_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var client1 = TestEnvironment.Clients.First();

        string HeroId = null;

        // Create new text objects for name fields for server to set
        var originalFullName = new TextObject("Test Name");
        var originalFirstName = new TextObject("Name");

        // Create new text objects for name fields for client to attempt to set
        // expected that it does not change
        var differentFullName = new TextObject("Dont set me");
        var differentFirstName = new TextObject("Dont set me");

        // Act
        server.Call(() =>
        {
            Hero hero = GameObjectCreator.CreateInitializedObject<Hero>();
            hero.SetName(originalFullName, originalFirstName);

            server.ObjectManager.TryGetId(hero, out HeroId);

            // Assert
            Assert.Equal(originalFullName.Value, hero.Name.Value);
            Assert.Equal(originalFirstName.Value, hero.FirstName.Value);

            Assert.NotEqual(differentFullName.Value, hero.Name.Value);
            Assert.NotEqual(differentFirstName.Value, hero.FirstName.Value);
        });

        client1.Call(() =>
        {
            client1.ObjectManager.TryGetObject(HeroId, out Hero hero);
            hero.SetName(differentFullName, differentFirstName);

            Assert.Equal(originalFullName.Value, hero.Name.Value);
            Assert.Equal(originalFirstName.Value, hero.FirstName.Value);

            Assert.NotEqual(differentFullName.Value, hero.Name.Value);
            Assert.NotEqual(differentFirstName.Value, hero.FirstName.Value);
        });
    }
}