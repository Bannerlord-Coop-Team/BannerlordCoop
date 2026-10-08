using Common.Messaging;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Messages;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsWeapons;

using Issue = GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue;
using Quest = GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest;

[QuestTypeModule]
internal static class GangLeaderNeedsWeaponsQuestType
{
    static GangLeaderNeedsWeaponsQuestType()
    {
        var strategy = new AcceptanceStrategy();
        QuestTypeRegistry.Register(QuestDescriptorBuilder.For<Issue, Quest>("GangLeaderNeedsWeapons")
            .WithQuestSolutionAccept(strategy)
            .WithAlternativeAccept(strategy)
            .WithCreationTrigger(issue => MessageBroker.Instance.Publish(issue, new GangLeaderWeaponsIssueCreated(issue)))
            .Build());
    }

    // The static descriptor must resolve the current session rather than retain its first container.
    private sealed class AcceptanceStrategy : IRaceArbitratedAcceptMirrorStrategy<GangLeaderWeaponsQuestFields>,
        IAlternativeAcceptMirrorStrategy<GangLeaderWeaponsAlternativeFields>
    {
        private static IGangLeaderWeaponsAcceptance Current
        {
            get
            {
                if (ContainerProvider.TryResolve<IGangLeaderWeaponsAcceptance>(out var acceptance)) return acceptance;
                throw new InvalidOperationException("Weapons quest acceptance requires an active session container.");
            }
        }

        public void ReplayQuestAccepted(Hero owner) => Current.ReplayQuestAccepted(owner);
        public bool TryCaptureQuestFields(Hero owner, out GangLeaderWeaponsQuestFields fields) =>
            Current.TryCaptureQuestFields(owner, out fields);
        public void MirrorQuestAccepted(Hero owner, GangLeaderWeaponsQuestFields fields) => Current.MirrorQuestAccepted(owner, fields);
        public void ReplayAlternativeAccepted(Hero owner) => Current.ReplayAlternativeAccepted(owner);
        public bool TryCaptureAlternativeFields(Hero owner, out GangLeaderWeaponsAlternativeFields fields) =>
            Current.TryCaptureAlternativeFields(owner, out fields);
        public void MirrorAlternativeAccepted(Hero owner, GangLeaderWeaponsAlternativeFields fields) => Current.MirrorAlternativeAccepted(owner, fields);
        public void RejectAcceptance(Hero owner) => Current.RejectAcceptance(owner);
    }
}
