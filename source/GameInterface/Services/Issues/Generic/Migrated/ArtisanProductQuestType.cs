using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Generic.Migrated;

using Issue = ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue;
using Quest = ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssueQuest;

[QuestTypeModule]
internal sealed class ArtisanProductQuestType : IRaceArbitratedAcceptMirrorStrategy<ArtisanProductQuestAcceptFields>,
    IAlternativeAcceptMirrorStrategy<ArtisanProductAlternativeAcceptFields>
{
    static ArtisanProductQuestType()
    {
        var strategy = new ArtisanProductQuestType();
        QuestTypeRegistry.Register(QuestDescriptorBuilder.For<Issue, Quest>("ArtisanCantSellProductsAtAFairPrice")
            .WithQuestSolutionAccept(strategy)
            .WithAlternativeAccept(strategy)
            .Build());
    }

    // The static descriptor outlives session containers, so resolve the current session at dispatch.
    private static IArtisanProductQuestAcceptance Acceptance
        => ContainerProvider.TryResolve<IArtisanProductQuestAcceptance>(out var acceptance) ? acceptance :
            throw new InvalidOperationException("Artisan acceptance service is unavailable");

    public void ReplayQuestAccepted(Hero owner) => Acceptance.ReplayQuestAccepted(owner);
    public bool TryCaptureQuestFields(Hero owner, out ArtisanProductQuestAcceptFields fields)
        => Acceptance.TryCaptureQuestFields(owner, out fields);
    public void MirrorQuestAccepted(Hero owner, ArtisanProductQuestAcceptFields fields) => Acceptance.MirrorQuestAccepted(owner, fields);
    public void ReplayAlternativeAccepted(Hero owner) => Acceptance.ReplayAlternativeAccepted(owner);
    public bool TryCaptureAlternativeFields(Hero owner, out ArtisanProductAlternativeAcceptFields fields)
        => Acceptance.TryCaptureAlternativeFields(owner, out fields);
    public void MirrorAlternativeAccepted(Hero owner, ArtisanProductAlternativeAcceptFields fields) => Acceptance.MirrorAlternativeAccepted(owner, fields);
    public void RejectAcceptance(Hero owner)
        => ((IRaceArbitratedAcceptMirrorStrategy<ArtisanProductQuestAcceptFields>)Acceptance).RejectAcceptance(owner);
}
