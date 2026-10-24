using Common;
using Common.Util;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.MerchantNeedsHelpWithOutlaws;
using Moq;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem.Party;
using Xunit;
using Quest = TaleWorlds.CampaignSystem.Issues.MerchantNeedsHelpWithOutlawsIssueQuestBehavior.MerchantNeedsHelpWithOutlawsIssueQuest;

namespace GameInterface.Tests.Services.Issues;

[Collection(ModInformationRoleCollection.Name)]
public class MerchantOutlawsHourlyTickTests : IDisposable
{
    private sealed class RecordingProgress : IMerchantOutlawsProgress
    {
        public int Calls { get; private set; }

        public void Send(Quest quest, bool final = false) => Calls++;

        public void Send(TaleWorlds.CampaignSystem.Issues.IssueBase issue) => Calls++;

        public void Forget(TaleWorlds.CampaignSystem.Issues.IssueBase issue) { }
    }

    private readonly bool wasServer = ModInformation.IsServer;

    public MerchantOutlawsHourlyTickTests() => ModInformation.IsServer = true;

    public void Dispose() => ModInformation.IsServer = wasServer;

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(500)]
    public void UnrelatedPartiesDoNotDispatchEnterOwnerScopesOrSendProgress(int count)
    {
        var dispatcher = new Mock<IOutcomeDispatcher>(MockBehavior.Strict);
        var progress = new RecordingProgress();
        var service = new MerchantOutlawsHourlyTick(dispatcher.Object, progress);
        var parties = Enumerable.Range(0, count).Select(_ => ObjectHelper.SkipConstructor<MobileParty>()).ToArray();

        service.Tick(parties);

        dispatcher.VerifyNoOtherCalls();
        Assert.Equal(0, progress.Calls);
    }

    [Fact]
    public void FiveHundredBanditsShareOneDispatchInsteadOfScanningIssuesPerParty()
    {
        var dispatcher = new Mock<IOutcomeDispatcher>();
        var progress = new RecordingProgress();
        var service = new MerchantOutlawsHourlyTick(dispatcher.Object, progress);
        var parties = Enumerable.Range(0, 500).Select(_ =>
        {
            var party = ObjectHelper.SkipConstructor<MobileParty>();
            party.IsBandit = true;
            return party;
        }).ToArray();

        service.Tick(parties);

        dispatcher.Verify(value => value.RunAsOwners(It.IsAny<Action<Quest>>()), Times.Once);
        dispatcher.VerifyNoOtherCalls();
        Assert.Equal(0, progress.Calls);
    }
}
