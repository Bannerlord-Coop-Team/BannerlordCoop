using Common.Util;
using E2E.Tests.Environment;
using GameInterface.Policies;
using GameInterface.Services.Issues.Framework.Gating;
using TaleWorlds.CampaignSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

public class PlayerSwitchQuestCancelGatePatchTests : IDisposable
{
    private readonly StubIssueQuestGenerator quests = new();
    private readonly StubIssuePreconditions preconditions = new(QuestScenario.IssueType);
    private readonly QuestScenario scenario;

    private E2ETestEnvironment TestEnvironment { get; }

    public PlayerSwitchQuestCancelGatePatchTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
        scenario = new QuestScenario(TestEnvironment);
    }

    public void Dispose()
    {
        PlayerSwitchQuestCancelGatePatch.SuppressForPlayerSwitch = false;
        TestEnvironment.Dispose();
        quests.Dispose();
        preconditions.Dispose();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OngoingQuestIsCancelledOnlyOutsideThePlayerSwitch(bool playerSwitch)
    {
        scenario.Accept(scenario.ClientA);
        var quest = scenario.QuestOf(scenario.ClientA);

        scenario.ClientA.Call(() =>
        {
            using (new AllowedThread())
            {
                quest.StartQuest();
            }

            using (CallOriginalPolicy.AllowOriginalsForCurrentOperation())
            {
                PlayerSwitchQuestCancelGatePatch.SuppressForPlayerSwitch = playerSwitch;
                try
                {
                    Campaign.Current.QuestManager.OnPlayerCharacterChanged(Hero.MainHero, Hero.MainHero, null, false);
                }
                finally
                {
                    PlayerSwitchQuestCancelGatePatch.SuppressForPlayerSwitch = false;
                }
            }

            Assert.Equal(playerSwitch, quest.IsOngoing);
        });
    }

    [Fact]
    public void PrefixOnlySkipsDuringPlayerSwitch()
    {
        Assert.True(PlayerSwitchQuestCancelGatePatch.Prefix());

        PlayerSwitchQuestCancelGatePatch.SuppressForPlayerSwitch = true;

        Assert.False(PlayerSwitchQuestCancelGatePatch.Prefix());
    }
}
