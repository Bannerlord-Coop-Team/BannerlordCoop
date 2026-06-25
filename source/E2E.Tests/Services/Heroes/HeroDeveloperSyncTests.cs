using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Heroes;

public class HeroDeveloperSyncTests : SyncTestBase
{
    public HeroDeveloperSyncTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void Server_HeroDeveloper_RawXpGain_AppliesOnServerAndClients()
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var expectedTotalXp = 0;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));

            expectedTotalXp = hero.HeroDeveloper.TotalXp + 25;
            hero.HeroDeveloper.GainRawXp(25f, false);
        });

        AssertHeroTotalXp(Server, heroId, expectedTotalXp);
        foreach (var client in Clients)
        {
            AssertHeroTotalXp(client, heroId, expectedTotalXp);
        }
    }

    [Fact]
    public void Server_HeroDeveloper_SetSkillXp_AppliesOnServerAndClients()
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var skillId = TestEnvironment.CreateRegisteredObject<SkillObject>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(Server.ObjectManager.TryGetObject<SkillObject>(skillId, out var skill));

            hero.HeroDeveloper.SetSkillXp(skill, 35f);
        });

        AssertHeroSkillXp(Server, heroId, skillId, 35f);
        foreach (var client in Clients)
        {
            AssertHeroSkillXp(client, heroId, skillId, 35f);
        }
    }

    [Fact]
    public void Server_HeroDeveloper_SkillLevelChange_AppliesOnServerAndClients()
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var skillId = TestEnvironment.CreateRegisteredObject<SkillObject>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(Server.ObjectManager.TryGetObject<SkillObject>(skillId, out var skill));

            hero.HeroDeveloper.ChangeSkillLevelFromXpChange(skill, 3, false);
        });

        AssertHeroSkillLevel(Server, heroId, skillId, 3);
        foreach (var client in Clients)
        {
            AssertHeroSkillLevel(client, heroId, skillId, 3);
        }
    }

    private static void AssertHeroTotalXp(EnvironmentInstance instance, string heroId, int expectedTotalXp)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));

            Assert.Equal(expectedTotalXp, hero.HeroDeveloper.TotalXp);
        });
    }

    private static void AssertHeroSkillXp(EnvironmentInstance instance, string heroId, string skillId, float expectedSkillXp)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(instance.ObjectManager.TryGetObject<SkillObject>(skillId, out var skill));

            Assert.True(hero.HeroDeveloper._skillXps.TryGetValue(skill, out var skillXp));
            Assert.Equal(expectedSkillXp, skillXp);
        });
    }

    private static void AssertHeroSkillLevel(EnvironmentInstance instance, string heroId, string skillId, int expectedSkillLevel)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(instance.ObjectManager.TryGetObject<SkillObject>(skillId, out var skill));

            Assert.Equal(expectedSkillLevel, hero.GetSkillValue(skill));
        });
    }
}