#if DEBUG
using GameInterface.Services.MapEvents.NavalScenarios;
using System;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents.NavalScenarios;

public class NavalEncounterMeetingWaitTests
{
    private static readonly DateTime Start = new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);

    private readonly NavalEncounterMeetingWait wait = new NavalEncounterMeetingWait(new NavalScenarioConfig(), Start.AddMinutes(2));

    [Fact]
    public void ShouldAnswer_WaitsUntilTheConversationStayedOpenForTheSettleTime()
    {
        Assert.False(wait.ShouldAnswer(Start, conversationOpen: true));
        Assert.False(wait.ShouldAnswer(Start + NavalEncounterMeetingWait.SettleTime - TimeSpan.FromMilliseconds(1), true));
        Assert.True(wait.ShouldAnswer(Start + NavalEncounterMeetingWait.SettleTime, true));
    }

    [Fact]
    public void ShouldAnswer_NeverAnswersWhileNoConversationIsOpen()
    {
        Assert.False(wait.ShouldAnswer(Start, conversationOpen: false));
        Assert.False(wait.ShouldAnswer(Start.AddSeconds(60), conversationOpen: false));
    }

    [Fact]
    public void ShouldAnswer_RestartsTheSettleTimeWhenTheConversationClosedInBetween()
    {
        wait.ShouldAnswer(Start, true);
        wait.ShouldAnswer(Start.AddSeconds(1), false);

        Assert.False(wait.ShouldAnswer(Start.AddSeconds(2), true));
        Assert.True(wait.ShouldAnswer(Start.AddSeconds(2) + NavalEncounterMeetingWait.SettleTime, true));
    }

    [Fact]
    public void Answered_CountsTheAnswerAndSettlesAgainBeforeTheNextOne()
    {
        wait.ShouldAnswer(Start, true);
        Assert.True(wait.ShouldAnswer(Start + NavalEncounterMeetingWait.SettleTime, true));

        wait.Answered();

        Assert.Equal(1, wait.Answers);
        Assert.False(wait.ShouldAnswer(Start + NavalEncounterMeetingWait.SettleTime + TimeSpan.FromMilliseconds(250), true));
    }
}
#endif
