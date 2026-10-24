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
        var progress = new Mock<IMerchantOutlawsProgress>(MockBehavior.Strict);
        var service = new MerchantOutlawsHourlyTick(dispatcher.Object, progress.Object);
        var parties = Enumerable.Range(0, count).Select(_ => ObjectHelper.SkipConstructor<MobileParty>()).ToArray();

        service.Tick(parties);

        dispatcher.VerifyNoOtherCalls();
        progress.VerifyNoOtherCalls();
    }

    [Fact]
    public void FiveHundredBanditsShareOneDispatchInsteadOfScanningIssuesPerParty()
    {
        var dispatcher = new Mock<IOutcomeDispatcher>();
        var progress = new Mock<IMerchantOutlawsProgress>(MockBehavior.Strict);
        var service = new MerchantOutlawsHourlyTick(dispatcher.Object, progress.Object);
        var parties = Enumerable.Range(0, 500).Select(_ =>
        {
            var party = ObjectHelper.SkipConstructor<MobileParty>();
            party.IsBandit = true;
            return party;
        }).ToArray();

        service.Tick(parties);

        dispatcher.Verify(value => value.RunAsOwners(It.IsAny<Action<Quest>>()), Times.Once);
        dispatcher.VerifyNoOtherCalls();
        progress.VerifyNoOtherCalls();
    }
}
