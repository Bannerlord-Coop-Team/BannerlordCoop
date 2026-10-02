using Common.Messaging;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Generic.Migrated.HeadmanNeedsToDeliverAHerd;

using Issue = HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue;
using Quest = HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest;

[QuestTypeModule]
internal static class HeadmanNeedsToDeliverAHerdQuestType
{
    internal const byte RejectionProof = 1;

    static HeadmanNeedsToDeliverAHerdQuestType()
    {
        var accept = new AcceptStrategy();
        QuestTypeRegistry.Register(QuestDescriptorBuilder.For<Issue, Quest>("HeadmanNeedsToDeliverAHerd")
            .WithCreationTrigger(issue => MessageBroker.Instance.Publish(issue, new HeadmanNeedsToDeliverAHerdIssueCreated(issue)))
            .WithQuestSolutionAccept<HeadmanHerdQuestAcceptFields>(accept)
            .WithAlternativeAccept<HeadmanHerdAlternativeAcceptFields>(accept)
            .WithQuestSuccessValidation((issue, party) => Resolve<IHeadmanHerdDeliveryInterface>().CanDeliver(issue.IssueQuest as Quest, party))
            .WithQuestSuccessConsequence(quest => Resolve<IHeadmanHerdDeliveryInterface>().Deliver(quest))
            .WithQuestFailProofCapture(_ => RejectionProof)
            .WithQuestFailValidation(CanReject)
            .WithQuestFailConsequence(quest => Resolve<IHeadmanHerdDeliveryInterface>().Reject(quest))
            .Build());
    }

    private static bool CanReject(Issue issue)
    {
        if (QuestFailProofContext.Current != RejectionProof
            || !Resolve<IHeadmanHerdQuestAuthority>().TryEnter(issue.IssueOwner, out var scope)) return false;
        using (scope)
            return Resolve<IHeadmanHerdDeliveryInterface>().CanReject(issue.IssueQuest as Quest, MobileParty.MainParty);
    }

    private static T Resolve<T>() where T : class
    {
        if (ContainerProvider.TryResolve<T>(out var service)) return service;
        throw new InvalidOperationException($"Deliver the Herd service {typeof(T).Name} is unavailable");
    }

    // Descriptors outlive campaign containers; resolve each acceptance against the current campaign.
    private sealed class AcceptStrategy : IHeadmanNeedsToDeliverAHerdAcceptInterface
    {
        public void ReplayQuestAccepted(Hero owner) => Resolve<IHeadmanNeedsToDeliverAHerdAcceptInterface>().ReplayQuestAccepted(owner);
        public bool TryCaptureQuestFields(Hero owner, out HeadmanHerdQuestAcceptFields fields)
            => Resolve<IHeadmanNeedsToDeliverAHerdAcceptInterface>().TryCaptureQuestFields(owner, out fields);
        public void MirrorQuestAccepted(Hero owner, HeadmanHerdQuestAcceptFields fields)
            => Resolve<IHeadmanNeedsToDeliverAHerdAcceptInterface>().MirrorQuestAccepted(owner, fields);
        public void ReplayAlternativeAccepted(Hero owner) => Resolve<IHeadmanNeedsToDeliverAHerdAcceptInterface>().ReplayAlternativeAccepted(owner);
        public bool TryCaptureAlternativeFields(Hero owner, out HeadmanHerdAlternativeAcceptFields fields)
            => Resolve<IHeadmanNeedsToDeliverAHerdAcceptInterface>().TryCaptureAlternativeFields(owner, out fields);
        public void MirrorAlternativeAccepted(Hero owner, HeadmanHerdAlternativeAcceptFields fields)
            => Resolve<IHeadmanNeedsToDeliverAHerdAcceptInterface>().MirrorAlternativeAccepted(owner, fields);
        public void RejectAcceptance(Hero owner) => Resolve<IHeadmanNeedsToDeliverAHerdAcceptInterface>().RejectAcceptance(owner);
    }
}
