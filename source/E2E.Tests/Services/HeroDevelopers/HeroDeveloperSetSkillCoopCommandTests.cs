using Common.Commands;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.HeroDevelopers.Commands;
using GameInterface.Utils.Commands;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.HeroDevelopers;

public class HeroDeveloperSetSkillCoopCommandTests : IDisposable
{
    private const string RidingSkillId = "set_skill_riding";

    private readonly E2ETestEnvironment testEnvironment;
    private EnvironmentInstance Server => testEnvironment.Server;
    private IEnumerable<EnvironmentInstance> Clients => testEnvironment.Clients;

    public HeroDeveloperSetSkillCoopCommandTests(ITestOutputHelper output)
    {
        testEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose() => testEnvironment.Dispose();

    [Fact]
    public void Riding_LoweredOnTheServer_SetsTheSkillAndXpAndReachesClients()
    {
        var heroId = testEnvironment.CreateRegisteredObject<Hero>();
        RegisterRidingSkill();

        Server.Call(() =>
        {
            Assert.True(Execute(Server, heroId, "Riding", "1023", "0").Succeeded);

            var result = Execute(Server, heroId, "Riding", "300", "1023");

            Assert.True(result.Succeeded, result.Output);
            Assert.Contains("\"before\":1023", result.Output);
            Assert.Contains("\"value\":300", result.Output);
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.Equal(300, hero.GetSkillValue(DefaultSkills.Riding));
            var xpForLevel = Campaign.Current.Models.CharacterDevelopmentModel.GetXpRequiredForSkillLevel(300);
            Assert.Equal(xpForLevel, hero.HeroDeveloper.GetSkillXp(DefaultSkills.Riding));
        });

        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(client.ObjectManager.TryGetObject<SkillObject>(RidingSkillId, out var riding));
            Assert.Equal(300, hero.GetSkillValue(riding));
        }
    }

    [Fact]
    public void Skill_WithAStaleExpectedLevel_IsRefused()
    {
        var heroId = testEnvironment.CreateRegisteredObject<Hero>();

        Server.Call(() =>
        {
            var result = Execute(Server, heroId, "OneHanded", "300", "50");

            Assert.False(result.Succeeded);
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.Equal(0, hero.GetSkillValue(DefaultSkills.OneHanded));
        });
    }

    [Theory]
    [InlineData("1024")]
    [InlineData("-1")]
    [InlineData("high")]
    public void Skill_WithALevelOutOfRange_IsRefused(string level)
    {
        var heroId = testEnvironment.CreateRegisteredObject<Hero>();

        Server.Call(() =>
        {
            var result = Execute(Server, heroId, "Bow", level, "0");

            Assert.False(result.Succeeded);
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.Equal(0, hero.GetSkillValue(DefaultSkills.Bow));
        });
    }

    [Fact]
    public void Skill_WithAnUnknownName_IsRefused()
    {
        var heroId = testEnvironment.CreateRegisteredObject<Hero>();

        Server.Call(() =>
        {
            var result = Execute(Server, heroId, "Horseback", "300", "0");

            Assert.False(result.Succeeded);
        });
    }

    [Fact]
    public void Skill_SetOnAClient_IsRefused()
    {
        var heroId = testEnvironment.CreateRegisteredObject<Hero>();
        var client = Clients.First();

        client.Call(() =>
        {
            var result = Execute(client, heroId, "Riding", "300", "0");

            Assert.False(result.Succeeded);
            Assert.True(client.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.Equal(0, hero.GetSkillValue(DefaultSkills.Riding));
        });
    }

    // The skill is the key of the synced skill values, so every instance needs it under the same id
    private void RegisterRidingSkill()
    {
        foreach (var instance in Clients.Prepend(Server))
            instance.Call(() =>
            {
                var handle = instance.Resolve<TestNetworkRouter>().GetOrCreateFixtureHandle(RidingSkillId);
                Assert.True(instance.ObjectManager.AddExisting(RidingSkillId, DefaultSkills.Riding, handle));
            });
    }

    private static CoopCommandResult Execute(EnvironmentInstance instance, params string[] args) =>
        new HeroDeveloperCommands.HeroDeveloperSetSkillCoopCommand(instance.Resolve<ICommandObjectResolver>(), instance.ObjectManager)
            .ProcessCommand(new CoopCommandArgsFactory().FromValues(args));
}
