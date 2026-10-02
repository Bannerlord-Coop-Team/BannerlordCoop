using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Messages;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Issues;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Surrogates;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

public class ExtortionQuestStateTests
{
    [Fact]
    public void RemovedDesertersCannotDriveWorldActionsOrCompleteTheOwnersQuest()
    {
        var quest = ObjectHelper.SkipConstructor<ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest>();
        quest._questState = QuestBase.QuestStates.Ongoing;
        quest._deserterMobileParty = ObjectHelper.SkipConstructor<MobileParty>();
        var ownership = new Mock<IIssueOwnershipRegistry>(MockBehavior.Strict);
        var players = new Mock<IPlayerManager>(MockBehavior.Strict);
        var objects = new Mock<IObjectManager>(MockBehavior.Strict);
        var context = new ExtortionQuestContext(ownership.Object, players.Object, objects.Object);
        var broker = new Mock<IMessageBroker>(MockBehavior.Strict);

        new ExtortionQuestWorld(context, broker.Object).Tick(quest);

        Assert.True(quest.IsOngoing);
        ownership.VerifyNoOtherCalls();
        players.VerifyNoOtherCalls();
        objects.VerifyNoOtherCalls();
        broker.VerifyNoOtherCalls();
    }

    [Fact]
    public void ProgressMessagePreservesIssueIdentityStatusAndJournalValues()
    {
        _ = new SurrogateCollection();
        var time = new CampaignTime(123456);
        var text = new TextObject("Returned {COUNT} soldiers.");
        text.SetTextVariable("COUNT", 8);
        var entry = new JournalLog(time, text, new TextObject("Return days"), 3, 5, LogType.Discreate);
        var message = new NetworkExtortionAlternativeState("giver-one", "issue-one", true,
            new[] { new ExtortionJournalEntry(entry) }, IssueBase.IssueUpdateDetails.SentTroopsFailedQuest);

        var result = GenericAcceptFieldsSerializer.Deserialize<NetworkExtortionAlternativeState>(
            GenericAcceptFieldsSerializer.Serialize(message));

        Assert.Equal("giver-one", result.GiverId);
        Assert.Equal("issue-one", result.IssueId);
        Assert.True(result.EffectsResolved);
        Assert.Equal(IssueBase.IssueUpdateDetails.SentTroopsFailedQuest, result.Status);
        var log = Assert.Single(result.Journal).ToJournalLog();
        Assert.Equal(time.NumTicks, log.LogTime.NumTicks);
        Assert.Equal("Returned 8 soldiers.", log.LogText.ToString());
        Assert.Equal("Return days", log.TaskName.ToString());
        Assert.Equal(3, log.CurrentProgress);
        Assert.Equal(5, log.Range);
        Assert.Equal(LogType.Discreate, log.Type);
    }
}
