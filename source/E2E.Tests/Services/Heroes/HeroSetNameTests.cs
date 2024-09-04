using E2E.Tests.Environment;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Heroes;

public class HeroSetNameTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }
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

        var networkId = "CoopHero_1";
        // Creates a new hero and registers it with the objectManager
        // using the networkId as an identifier
        var serverHero = TestEnvironment.Server.CreateRegisteredObject<Hero>(networkId);
        
        // Creates and stores heroes on the clients with same id as server
        var clientHeroes = new List<Hero>();
        foreach(var client in TestEnvironment.Clients)
        {
            clientHeroes.Add(client.CreateRegisteredObject<Hero>(networkId));
        }

        // Create new text objects for name fields
        var fullName = new TextObject("Test Name");
        var firstName = new TextObject("Name");

        // Act
        server.Call(() =>
        {
            serverHero.SetName(fullName, firstName);
        });

        // Assert
        Assert.Equal(fullName.Value, serverHero.Name.Value);
        Assert.Equal(firstName.Value, serverHero.FirstName.Value);


        foreach (var clientHero in clientHeroes)
        {
            Assert.Equal(fullName.Value, clientHero.Name.Value);
            Assert.Equal(firstName.Value, clientHero.FirstName.Value);
        }
    }

    [Fact]
    public void ClientSetName_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var client1 = TestEnvironment.Clients.First();

        var networkId = "CoopHero_1";

        // Creates a new hero and registers it with the objectManager
        // using the networkId as an identifier
        var serverHero = TestEnvironment.Server.CreateRegisteredObject<Hero>(networkId);

        // Creates and stores heroes on the clients with same id as server
        var clientHeroes = new List<Hero>();
        foreach (var client in TestEnvironment.Clients)
        {
            clientHeroes.Add(client.CreateRegisteredObject<Hero>(networkId));
        }

        // Create new text objects for name fields for server to set
        var originalFullName = new TextObject("Test Name");
        var originalFirstName = new TextObject("Name");

        server.Call(() =>
        {
            serverHero.SetName(originalFullName, originalFirstName);
        });

        // Create new text objects for name fields for client to attempt to set
        // expected that it does not change
        var differentFullName = new TextObject("Dont set me");
        var differentFirstName = new TextObject("Dont set me");

        // Act
        client1.Call(() =>
        {
            clientHeroes.First().SetName(differentFullName, differentFirstName);
        });

        // Assert
        Assert.Equal(originalFullName.Value, serverHero.Name.Value);
        Assert.Equal(originalFirstName.Value, serverHero.FirstName.Value);

        Assert.NotEqual(differentFullName.Value, serverHero.Name.Value);
        Assert.NotEqual(differentFirstName.Value, serverHero.FirstName.Value);

        foreach (var clientHero in clientHeroes)
        {
            Assert.Equal(originalFullName.Value, clientHero.Name.Value);
            Assert.Equal(originalFirstName.Value, clientHero.FirstName.Value);

            Assert.NotEqual(differentFullName.Value, clientHero.Name.Value);
            Assert.NotEqual(differentFirstName.Value, clientHero.FirstName.Value);
        }
    }
}