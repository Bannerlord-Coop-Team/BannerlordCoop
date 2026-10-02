using Common.Messaging;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Generic.Dispatch;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Generic.Migrated.TheConquestOfSettlement;

using Issue = TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue;
using Quest = TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssueQuest;

internal interface IConquestQuest : IRaceArbitratedAcceptMirrorStrategy<ConquestAcceptFields>
{
    bool TryOpenOwnerScope(Hero giver, out IDisposable scope);
    bool HasQuest(Hero hero);
    bool IsOwnedBy(Hero giver, Hero hero);
    bool IsVisible(QuestBase quest);
    bool IsVisible(JournalLogEntry entry);
    void CancelForPlayerRemoval(string controllerId);
}

internal sealed class ConquestQuest : IConquestQuest
{
    [ThreadStatic]
    internal static Quest CancellingRemovedPlayerQuest;

    private readonly IIssueOwnershipRegistry ownership;
    private readonly IPlayerManager players;
    private readonly IObjectManager objects;
    private readonly IIssueGenerationRegistry generations;
    private readonly IControllerIdProvider controller;

    public ConquestQuest(IIssueOwnershipRegistry ownership, IPlayerManager players, IObjectManager objects,
        IIssueGenerationRegistry generations, IControllerIdProvider controller)
    {
        this.ownership = ownership;
        this.players = players;
        this.objects = objects;
        this.generations = generations;
        this.controller = controller;
    }

    public bool TryOpenOwnerScope(Hero giver, out IDisposable scope)
    {
        scope = null;
        return ownership.TryGetOwnerControllerId(giver, out var controllerId) && TryOpenScope(controllerId, out scope);
    }

    private bool TryOpenScope(string controllerId, out IDisposable scope)
    {
        scope = null;
        if (!players.TryGetPlayer(controllerId, out var player)) return false;
        if (!objects.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero)) return false;
        if (!objects.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party)) return false;
        scope = new MainHeroSubstitutionScope(hero, party);
        return true;
    }

    public bool HasQuest(Hero hero)
    {
        return Campaign.Current.IssueManager.Issues.Values.Any(issue =>
            issue is Issue && issue.IsSolvingWithQuest && IsOwnedBy(issue.IssueOwner, hero));
    }

    public bool IsOwnedBy(Hero giver, Hero hero)
    {
        return hero != null && ownership.TryGetOwnerControllerId(giver, out var controllerId) &&
            players.TryGetPlayer(controllerId, out var player) &&
            objects.TryGetObject<Hero>(player.HeroId, out var owner) && owner == hero;
    }

    public bool IsVisible(QuestBase quest)
    {
        return ownership.TryGetQuestOwner(quest.StringId, out var ownerId) && ownerId == controller.ControllerId;
    }

    public bool IsVisible(JournalLogEntry entry)
    {
        return entry._relatedObjectIds.Any(id =>
            ownership.TryGetQuestOwner(id, out var ownerId) && ownerId == controller.ControllerId);
    }

    public void ReplayQuestAccepted(Hero owner)
    {
        if (owner?.Issue is not Issue issue || !issue.IsOngoingWithoutQuest) return;
        if (!generations.TryGetGeneration(owner, out _)) return;
        var player = players.Players.FirstOrDefault(candidate =>
            objects.TryGetObject<Hero>(candidate.HeroId, out var hero) && hero == Hero.MainHero &&
            objects.TryGetObject<MobileParty>(candidate.MobilePartyId, out var party) && party == MobileParty.MainParty);
        if (player == null) return;
        if (!issue.IssueStayAliveConditions() || !issue.CheckPreconditions(owner, out _)) return;

        using (new IssueDispatchReplayGuard())
        {
            ownership.SetOwner(owner, player.ControllerId);
            ownership.SetQuestOwner(issue.StringId + "_quest", player.ControllerId);
            try
            {
                Campaign.Current.IssueManager.StartIssueQuest(owner);
                if (issue.IssueQuest is not Quest quest) throw new InvalidOperationException("Conquest quest was not created");
                quest.QuestAcceptedConsequences();
            }
            catch
            {
                using (new IssueFinalizeAuthorityGuard())
                {
                    if (issue.IssueQuest?.IsOngoing == true) issue.IssueQuest.CompleteQuestWithCancel();
                    else issue.IssueFinalized();
                }
                throw;
            }
        }
    }

    public bool TryCaptureQuestFields(Hero owner, out ConquestAcceptFields fields)
    {
        fields = default;
        if (owner?.Issue?.IssueQuest is not Quest quest || !quest.IsOngoing || quest.JournalEntries.Count == 0) return false;
        if (!ownership.TryGetOwnerControllerId(owner, out var controllerId)) return false;
        if (!generations.TryGetGeneration(owner, out var generation)) return false;

        fields = new ConquestAcceptFields(controllerId, quest.QuestDueTime, quest.JournalEntries[0].LogTime, quest.StringId, generation);
        return true;
    }

    public void MirrorQuestAccepted(Hero owner, ConquestAcceptFields fields)
    {
        if (owner?.Issue is not Issue issue || fields.QuestId != issue.StringId + "_quest" ||
            !generations.TryGetGeneration(owner, out var generation) || generation != fields.Generation)
            throw new InvalidOperationException("Conquest acceptance does not match the current issue");
        if (!TryOpenScope(fields.ControllerId, out var scope)) throw new InvalidOperationException("Conquest quest owner is unavailable");

        using (scope)
        using (new AllowedThread())
        using (new IssueDispatchReplayGuard())
        using (new QuestSolutionStartAuthorityGuard())
        {
            ownership.SetOwner(owner, fields.ControllerId);
            ownership.SetQuestOwner(fields.QuestId, fields.ControllerId);
            if (issue.IsOngoingWithoutQuest) Campaign.Current.IssueManager.StartIssueQuest(owner);
            if (issue.IssueQuest is not Quest quest) throw new InvalidOperationException("Conquest quest mirror was not created");
            quest.ChangeQuestDueTime(fields.DueTime);
            if (quest.JournalEntries.Count != 0) return;
            quest.QuestAcceptedConsequences();
            var startLog = quest.JournalEntries[0];
            quest._journalEntries[0] = new JournalLog(fields.StartTime, startLog.LogText);
        }
    }

    public void RejectAcceptance(Hero owner)
    {
        // Acceptance is deferred until arbitration, so a rejected client has no speculative quest.
    }

    public void CancelForPlayerRemoval(string controllerId)
    {
        var quests = Campaign.Current.QuestManager.Quests.OfType<Quest>().Where(quest => quest.IsOngoing &&
            ownership.TryGetQuestOwner(quest.StringId, out var ownerId) && ownerId == controllerId).ToArray();
        var previous = CancellingRemovedPlayerQuest;
        try
        {
            using (new IssueFinalizeAuthorityGuard())
            {
                foreach (var quest in quests)
                {
                    CancellingRemovedPlayerQuest = quest;
                    quest.CompleteQuestWithCancel(new TextObject("{=coop_conquest_owner_removed}The quest was canceled because the character who accepted it is no longer available."));
                }
            }
        }
        finally
        {
            CancellingRemovedPlayerQuest = previous;
        }
    }
}

[QuestTypeModule]
internal static class TheConquestOfSettlementQuestType
{
    internal static IConquestQuest Service => ContainerProvider.TryResolve<IConquestQuest>(out var service)
        ? service : throw new InvalidOperationException("Conquest quest service is unavailable");

    private sealed class AcceptStrategy : IRaceArbitratedAcceptMirrorStrategy<ConquestAcceptFields>
    {
        public void ReplayQuestAccepted(Hero owner) => Service.ReplayQuestAccepted(owner);
        public bool TryCaptureQuestFields(Hero owner, out ConquestAcceptFields fields) => Service.TryCaptureQuestFields(owner, out fields);
        public void MirrorQuestAccepted(Hero owner, ConquestAcceptFields fields) => Service.MirrorQuestAccepted(owner, fields);
        public void RejectAcceptance(Hero owner) => Service.RejectAcceptance(owner);
    }

    static TheConquestOfSettlementQuestType()
    {
        QuestTypeRegistry.Register(QuestDescriptorBuilder.For<Issue, Quest>("TheConquestOfSettlement")
            .WithQuestSolutionAccept(new AcceptStrategy())
            .WithCreationTrigger(issue => MessageBroker.Instance.Publish(issue, new ConquestIssueCreated(issue)))
            .Build());
    }
}
