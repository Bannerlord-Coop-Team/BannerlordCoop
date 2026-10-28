using Common.Commands;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.HeroDevelopers.Commands;
using GameInterface.Services.ObjectManager;
using GameInterface.Utils.Commands;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
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

        testEnvironment.FlushCoalescer();

        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(client.ObjectManager.TryGetObject<SkillObject>(RidingSkillId, out var riding));
            Assert.Equal(300, hero.GetSkillValue(riding));
            client.Call(() =>
                Assert.Equal(
                    Campaign.Current.Models.CharacterDevelopmentModel.GetXpRequiredForSkillLevel(300),
                    hero.HeroDeveloper.GetSkillXp(riding)));
        }
    }

    [Fact]
    public void Riding_SetToZeroOnTheServer_ClearsTheXpOnClients()
    {
        var heroId = testEnvironment.CreateRegisteredObject<Hero>();
        RegisterRidingSkill();

        Server.Call(() =>
        {
            Assert.True(Execute(Server, heroId, "Riding", "300", "0").Succeeded);
            var result = Execute(Server, heroId, "Riding", "0", "300");

            Assert.True(result.Succeeded, result.Output);
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.Equal(0f, hero.HeroDeveloper.GetSkillXp(DefaultSkills.Riding));
        });

        testEnvironment.FlushCoalescer();

        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(client.ObjectManager.TryGetObject<SkillObject>(RidingSkillId, out var riding));
            Assert.Equal(0, hero.GetSkillValue(riding));
            Assert.Equal(0f, hero.HeroDeveloper.GetSkillXp(riding));
        }
    }

    [Fact]
    public void Riding_WithXpNearIntMax_IsLoweredAndStaysLoweredAfterXpGain()
    {
        var heroId = testEnvironment.CreateRegisteredObject<Hero>();
        RegisterRidingSkill();
        var xpForLevel = 0;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            hero.SetSkillValue(DefaultSkills.Riding, 1023);
            hero.HeroDeveloper.SetSkillXp(DefaultSkills.Riding, 1_066_984_724f);

            var result = Execute(Server, heroId, "Riding", "300", "1023");
            hero.HeroDeveloper.AddSkillXp(DefaultSkills.Riding, 100f, isAffectedByFocusFactor: false, shouldNotify: false);

            Assert.True(result.Succeeded, result.Output);
            Assert.Equal(300, hero.GetSkillValue(DefaultSkills.Riding));
            xpForLevel = Campaign.Current.Models.CharacterDevelopmentModel.GetXpRequiredForSkillLevel(300);
            Assert.InRange(hero.HeroDeveloper.GetSkillXp(DefaultSkills.Riding), xpForLevel, xpForLevel + 100f);
        });

        testEnvironment.FlushCoalescer();

        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(client.ObjectManager.TryGetObject<SkillObject>(RidingSkillId, out var riding));
            Assert.Equal(300, hero.GetSkillValue(riding));
            Assert.InRange(hero.HeroDeveloper.GetSkillXp(riding), xpForLevel, xpForLevel + 100f);
        }
    }

    [Fact]
    public void ModSkill_SetOnTheServer_IsFoundByItsStringIdInAnyCase()
    {
        var heroId = testEnvironment.CreateRegisteredObject<Hero>();

        Server.Call(() =>
        {
            // The harness leaves War Sails out, so stand Mariner in the way the DLC registers it.
            // Game start skills keep their StringId, the server auto registry would rename this one to Created_N.
            SkillObject mariner;
            using (new AllowedThread())
            {
                mariner = MBObjectManager.Instance.RegisterPresumedObject(new SkillObject("Mariner"));
            }

            var result = Execute(Server, heroId, "mariner", "50", "0");

            Assert.True(result.Succeeded, result.Output);
            Assert.Contains("\"skill\":\"Mariner\"", result.Output);
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.Equal(50, hero.GetSkillValue(mariner));
        });
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
            Assert.Contains("OneHanded", result.Output);
            Assert.Contains("Riding", result.Output);
        });
    }

    [Fact]
    public void Skill_ForAnUnknownHero_IsRefused()
    {
        var heroId = testEnvironment.CreateRegisteredObject<Hero>();

        Server.Call(() =>
        {
            var result = Execute(Server, "set_skill_no_such_hero", "Riding", "300", "0");

            Assert.False(result.Succeeded);
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.Equal(0, hero.GetSkillValue(DefaultSkills.Riding));
        });
    }

    [Fact]
    public void Skill_ForAnUnregisteredHero_IsRefused()
    {
        var heroId = testEnvironment.CreateRegisteredObject<Hero>();

        Server.Call(() =>
        {
            // The real resolver still finds the hero, but this object manager has no id for it
            var result = new HeroDeveloperCommands.HeroDeveloperSetSkillCoopCommand(Server.Resolve<ICommandObjectResolver>(), Mock.Of<IObjectManager>())
                .ProcessCommand(new CoopCommandArgsFactory().FromValues(new[] { heroId, "Riding", "300", "0" }));

            Assert.False(result.Succeeded);
            Assert.Contains("not registered", result.Output);
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.Equal(0, hero.GetSkillValue(DefaultSkills.Riding));
        });
    }

    [Fact]
    public void Skill_ForAHeroWithoutAHeroDeveloper_IsRefused()
    {
        var heroId = testEnvironment.CreateRegisteredObject<Hero>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            hero._heroDeveloper = null;

            var result = Execute(Server, heroId, "Riding", "300", "0");

            Assert.False(result.Succeeded);
            Assert.Contains("no hero developer", result.Output);
            Assert.Equal(0, hero.GetSkillValue(DefaultSkills.Riding));
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
