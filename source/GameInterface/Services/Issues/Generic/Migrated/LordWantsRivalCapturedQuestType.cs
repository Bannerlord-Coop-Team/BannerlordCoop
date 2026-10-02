using Common.Messaging;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Generic.Migrated;

using Issue = LordWantsRivalCapturedIssueBehavior.LordWantsRivalCapturedIssue;
using Quest = LordWantsRivalCapturedIssueBehavior.LordWantsRivalCapturedIssueQuest;

[QuestTypeModule]
internal static class LordWantsRivalCapturedQuestType
{
    private sealed class AcceptMirror : IRaceArbitratedAcceptMirrorStrategy<RivalCapturedAcceptFields>
    {
        private ILordWantsRivalCapturedQuestService Service
        {
            get
            {
                if (ContainerProvider.TryResolve<ILordWantsRivalCapturedQuestService>(out var service)) return service;
                throw new InvalidOperationException("Rival captured quest service is not registered");
            }
        }

        public void ReplayQuestAccepted(Hero owner) =>
            Service.ReplayQuestAccepted(owner);

        public bool TryCaptureQuestFields(Hero owner, out RivalCapturedAcceptFields fields) =>
            Service.TryCaptureQuestFields(owner, out fields);

        public void MirrorQuestAccepted(Hero owner, RivalCapturedAcceptFields fields) =>
            Service.MirrorQuestAccepted(owner, fields);

        public void RejectAcceptance(Hero owner) { }
    }

    static LordWantsRivalCapturedQuestType()
    {
        QuestTypeRegistry.Register(QuestDescriptorBuilder.For<Issue, Quest>("LordWantsRivalCaptured")
            .WithQuestSolutionAccept(new AcceptMirror())
            .WithCreationTrigger(issue => MessageBroker.Instance.Publish(issue, new RivalCapturedIssueCreated(issue)))
            .Build());
    }
}
