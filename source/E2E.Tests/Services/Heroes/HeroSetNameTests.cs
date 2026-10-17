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
    public void ServerSetName_KeepsNameAttributesOnClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        string heroId = null;

        var firstName = new TextObject("{=!}Lucon");
        var fullName = new TextObject("{=!}{FIRSTNAME} {CLAN_NAME}", new Dictionary<string, object>
        {
            ["FIRSTNAME"] = new TextObject("{=!}Lucon"),
            ["CLAN_NAME"] = "dey Meroc",
        });

        // Act
        server.Call(() =>
        {
            Hero hero = GameObjectCreator.CreateInitializedObject<Hero>();
            hero.SetName(fullName, firstName);

            Assert.True(server.ObjectManager.TryGetId(hero, out heroId));
        });

        // Assert
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject(heroId, out Hero hero));

            Assert.Equal(fullName.Value, hero.Name.Value);
            Assert.Equal(firstName.Value, hero.FirstName.Value);

            Assert.NotNull(hero.Name.Attributes);
            Assert.Equal("{=!}Lucon", Assert.IsType<TextObject>(hero.Name.Attributes["FIRSTNAME"]).Value);
            Assert.Equal("dey Meroc", hero.Name.Attributes["CLAN_NAME"]);
        }
    }
}