using E2E.Tests.Util;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.HeroDevelopers;

/// <summary>
/// v1.5 gives eligible heroes daily skill xp but leaves out the player's own hero. In co-op every
/// player hero is left out, not only the server's main hero.
/// </summary>
public class HeroDailyXpTests : SyncTestBase
{
    public HeroDailyXpTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void DailyTickHero_SkipsPlayerHeroesAndKeepsAiLords()
    {
        var playerHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var lordId = TestEnvironment.CreateRegisteredObject<Hero>();

        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(
                new Player("PlayerOne", playerHeroId, string.Empty, string.Empty, string.Empty)));
            var playerHero = Server.GetRegisteredObject<Hero>(playerHeroId);
            var lord = Server.GetRegisteredObject<Hero>(lordId);
            MakeEligibleLord(playerHero);
            MakeEligibleLord(lord);
            float playerXpBefore = playerHero.HeroDeveloper.GetSkillXp(DefaultSkills.OneHanded);
            float lordXpBefore = lord.HeroDeveloper.GetSkillXp(DefaultSkills.OneHanded);

            HeroDailyXpCampaignBehavior.DailyTickHero(playerHero);
            HeroDailyXpCampaignBehavior.DailyTickHero(lord);

            Assert.Equal(playerXpBefore, playerHero.HeroDeveloper.GetSkillXp(DefaultSkills.OneHanded));
            Assert.True(lord.HeroDeveloper.GetSkillXp(DefaultSkills.OneHanded) > lordXpBefore);
        });
    }

    private static void MakeEligibleLord(Hero hero)
    {
        hero.SetNewOccupation(Occupation.Lord);
        hero.SetBirthDay(CampaignTime.YearsFromNow(-30f));
        hero.HeroDeveloper.AddFocus(DefaultSkills.OneHanded, 1, checkUnspentFocusPoints: false);
        Assert.True(HeroDailyXpCampaignBehavior.IsEligibleForDailyXp(hero));
    }
}
