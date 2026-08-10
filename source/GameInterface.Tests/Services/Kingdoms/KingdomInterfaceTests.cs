using Autofac;
using Common;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Kingdoms;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using Xunit;
using KingdomDecisionType = TaleWorlds.CampaignSystem.Election.KingdomDecision;

namespace GameInterface.Tests.Services.Kingdoms;

[Collection(ModInformationRoleCollection.Name)]
public class KingdomInterfaceTests
{
    [Fact]
    public void ClientSettlementClaimantDecision_IsNotAppliedLocally()
    {
        var voteManager = new Mock<IKingdomDecisionVoteManager>();
        var kingdomInterface = new KingdomInterface(voteManager.Object);
        Kingdom kingdom = ObjectHelper.SkipConstructor<Kingdom>();
        SettlementClaimantDecision decision = ObjectHelper.SkipConstructor<SettlementClaimantDecision>();
        bool wasServer = ModInformation.IsServer;
        var builder = new ContainerBuilder();
        builder.RegisterInstance(new DenyOriginalSyncPolicy()).As<ISyncPolicy>();
        using var container = builder.Build();
        bool hadPreviousContainer = ContainerProvider.TryGetContainer(out var previousContainer);

        try
        {
            ModInformation.IsServer = false;

            using (ContainerProvider.UseContainerThreadSafe(container))
            {
                Assert.False(kingdomInterface.AddDecisionPrefix(kingdom, decision, true));
            }
        }
        finally
        {
            ModInformation.IsServer = wasServer;
            if (hadPreviousContainer)
            {
                ContainerProvider.SetContainer(previousContainer);
            }
            else
            {
                ContainerProvider.Clear();
            }
        }

        voteManager.Verify(
            value => value.HasEligiblePlayerClan(It.IsAny<KingdomDecisionType>()),
            Times.Never);
    }

    private sealed class DenyOriginalSyncPolicy : ISyncPolicy
    {
        public bool AllowOriginal() => false;
    }
}
