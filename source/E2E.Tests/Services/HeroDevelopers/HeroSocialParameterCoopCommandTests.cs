using Common.Commands;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.HeroDevelopers.Commands;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using Xunit.Abstractions;

namespace E2E.Tests.Services.HeroDevelopers;

public class HeroSocialParameterCoopCommandTests : IDisposable
{
    private readonly E2ETestEnvironment testEnvironment;
    private EnvironmentInstance Server => testEnvironment.Server;
    private IEnumerable<EnvironmentInstance> Clients => testEnvironment.Clients;

    public HeroSocialParameterCoopCommandTests(ITestOutputHelper output)
    {
        testEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose() => testEnvironment.Dispose();

    [Fact]
    public void Oratory_WrittenOnTheServer_OpensThePerk()
    {
        var heroId = testEnvironment.CreateRegisteredObject<Hero>();

        Server.Call(() =>
        {
            var result = Execute(heroId, "oratory", "1", "0");

            Assert.True(result.Succeeded, result.Output);
            Assert.Contains("\"before\":0", result.Output);
            Assert.Contains("\"value\":1", result.Output);
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(hero.GetPerkValue(DefaultPerks.Charm.Oratory));
        });
    }

    [Fact]
    public void Oratory_Read_ReportsTheCurrentValue()
    {
        var heroId = testEnvironment.CreateRegisteredObject<Hero>();

        Server.Call(() =>
        {
            Assert.True(Execute(heroId, "oratory", "1", "0").Succeeded);

            var result = Execute(heroId, "oratory");

            Assert.True(result.Succeeded, result.Output);
            Assert.Contains("\"before\":1", result.Output);
        });
    }

    [Fact]
    public void Oratory_WithAStaleExpectedValue_IsRefused()
    {
        var heroId = testEnvironment.CreateRegisteredObject<Hero>();

        Server.Call(() =>
        {
            var result = Execute(heroId, "oratory", "1", "1");

            Assert.False(result.Succeeded);
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.False(hero.GetPerkValue(DefaultPerks.Charm.Oratory));
        });
    }

    [Fact]
    public void Oratory_WrittenOnAClient_IsRefused()
    {
        var heroId = testEnvironment.CreateRegisteredObject<Hero>();
        var client = Clients.First();

        client.Call(() =>
        {
            var result = Execute(heroId, "oratory", "1", "0");

            Assert.False(result.Succeeded);
            Assert.True(client.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.False(hero.GetPerkValue(DefaultPerks.Charm.Oratory));
        });
    }

    private static CoopCommandResult Execute(params string[] args) =>
        new HeroDeveloperCommands.HeroSocialParameterCoopCommand().ProcessCommand(new CoopCommandArgsFactory().FromValues(args));
}
