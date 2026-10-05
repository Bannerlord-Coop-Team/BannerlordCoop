using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Handlers;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using Moq;
using GameInterface.Surrogates;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

[Collection(ModInformationRoleCollection.Name)]
public class HeadmanNeedsToDeliverAHerdCreationTests
{
    static HeadmanNeedsToDeliverAHerdCreationTests()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(Coop.Tests.Mocks.TestNetwork).Module.ModuleHandle);
    }

    [Fact]
    public void AcceptancePayloadFreezesJournalTimeProgressAndAlternativeTerms()
    {
        _ = new SurrogateCollection();
        var start = new HeadmanHerdJournalEntryData(new JournalLog(new CampaignTime(12345),
            new TextObject("the herd"), new TextObject("return days"), 2, 13, LogType.Text));
        var fields = new HeadmanHerdAlternativeAcceptFields(0.75f,
            new AlternativeSolutionVanillaState(new CampaignTime(54321), new CampaignTime(40000), 0, 0, 1375, "riding"), start);
        var received = GenericAcceptFieldsSerializer.Deserialize<HeadmanHerdAlternativeAcceptFields>(GenericAcceptFieldsSerializer.Serialize(fields));
        Assert.Equal(fields.Difficulty, received.Difficulty);
        Assert.Equal(54321, received.State.ReturnTime.NumTicks);
        Assert.Equal(40000, received.State.EffectClearTime.NumTicks);
        Assert.Equal(1375, received.State.TotalTroopXpAmount);
        Assert.Equal("riding", received.State.CompanionRewardSkillId);
        var log = received.StartLog.ToLog();
        Assert.Equal(12345, log.LogTime.NumTicks);
        Assert.Equal("the herd", log.LogText.ToString());
        Assert.Equal("return days", log.TaskName.ToString());
        Assert.Equal(2, log.CurrentProgress);
        Assert.Equal(13, log.Range);
        Assert.Equal(LogType.Text, log.Type);
    }

    [Theory]
    [InlineData(4, 4)]
    [InlineData(5, 4)]
    public void ReceivedCreationCannotResurrectAnAlreadyObservedGeneration(int current, int received)
    {
        var originalRole = ModInformation.IsServer;
        ModInformation.IsServer = false;
        try
        {
            var owner = ObjectHelper.SkipConstructor<Hero>();
            var objects = new Mock<IObjectManager>(MockBehavior.Strict);
            objects.Setup(x => x.TryGetObjectWithLogging("giver", out owner)).Returns(true);
            var generations = new IssueGenerationRegistry();
            generations.SetGeneration(owner, current);
            var issueInterface = new Mock<IHeadmanNeedsToDeliverAHerdIssueInterface>(MockBehavior.Strict);
            using var broker = new MessageBroker();
            using var handler = new HeadmanNeedsToDeliverAHerdIssueHandler(
                broker, objects.Object, Mock.Of<INetwork>(), issueInterface.Object, generations);

            broker.Publish(this, new NetworkHeadmanNeedsToDeliverAHerdIssueCreated(
                "giver", "destination", "target", "sheep", received, "issue_30", 123, 456));
            GameThread.Run(() => { }, blocking: true);

            objects.Verify(x => x.TryGetObjectWithLogging("giver", out owner), Times.Once);
            objects.VerifyNoOtherCalls();
            issueInterface.VerifyNoOtherCalls();
            Assert.True(generations.TryGetGeneration(owner, out var remaining));
            Assert.Equal(current, remaining);
        }
        finally
        {
            ModInformation.IsServer = originalRole;
        }
    }

    [Fact]
    public void CreationWirePayloadPreservesAuthoritativeIdentitiesAndGeneration()
    {
        var message = new NetworkHeadmanNeedsToDeliverAHerdIssueCreated(
            "giver", "town_ES1", "destination-notable", "cow", 17, "issue_42", 789, 987);

        var bytes = GenericAcceptFieldsSerializer.Serialize(message);
        var received = GenericAcceptFieldsSerializer.Deserialize<NetworkHeadmanNeedsToDeliverAHerdIssueCreated>(bytes);

        Assert.IsAssignableFrom<IServerToClientCommand>(received);
        Assert.Equal(message.OwnerId, received.OwnerId);
        Assert.Equal(message.TargetSettlementId, received.TargetSettlementId);
        Assert.Equal(message.TargetHeroId, received.TargetHeroId);
        Assert.Equal(message.HerdTypeToDeliverId, received.HerdTypeToDeliverId);
        Assert.Equal(message.Generation, received.Generation);
        Assert.Equal(message.IssueId, received.IssueId);
        Assert.Equal(message.CreationTimeTicks, received.CreationTimeTicks);
        Assert.Equal(message.DueTimeTicks, received.DueTimeTicks);
    }
}
