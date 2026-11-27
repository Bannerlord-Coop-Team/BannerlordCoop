using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Issues.Framework.Dismissal;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Framework.Visibility;
using GameInterface.Services.ObjectManager;
using ProtoBuf;
using System.Linq;
using TaleWorlds.CampaignSystem;
using Issue = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssue;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsGrain;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkHeadmanNeedsGrainEffectsResolved : ICommand
{
    [ProtoMember(1)] public readonly string GiverId;
    [ProtoMember(2)] public readonly string IssueId;

    public NetworkHeadmanNeedsGrainEffectsResolved(string giverId, string issueId)
    {
        GiverId = giverId;
        IssueId = issueId;
    }
}

internal class HeadmanNeedsGrainJournalHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly IObjectManager objectManager;
    private readonly IIssueConversationGuard conversationGuard;
    private readonly INetwork network;

    public HeadmanNeedsGrainJournalHandler(IMessageBroker broker, IObjectManager objectManager,
        IIssueConversationGuard conversationGuard, INetwork network)
    {
        this.broker = broker;
        this.objectManager = objectManager;
        this.conversationGuard = conversationGuard;
        this.network = network;
        broker.Subscribe<NetworkHeadmanNeedsGrainEffectsResolved>(HandleEffectsResolved);
        broker.Subscribe<IssueManagerDailyTicked>(SendResolvedEffects);
        broker.Subscribe<AlternativeTroopsReturned>(SendReturnedCompanionStates);
    }

    public void Dispose()
    {
        broker.Unsubscribe<NetworkHeadmanNeedsGrainEffectsResolved>(HandleEffectsResolved);
        broker.Unsubscribe<IssueManagerDailyTicked>(SendResolvedEffects);
        broker.Unsubscribe<AlternativeTroopsReturned>(SendReturnedCompanionStates);
    }

    // A client whose own daily tick misses the clearing day still clears the village's effects
    private void SendResolvedEffects(MessagePayload<IssueManagerDailyTicked> payload)
    {
        foreach (var issue in Campaign.Current.IssueManager.Issues.Values.OfType<Issue>())
        {
            if (issue.IsSolvingWithAlternative && issue._areIssueEffectsResolved &&
                objectManager.TryGetIdWithLogging(issue.IssueOwner, out var giverId))
                network.SendAll(new NetworkHeadmanNeedsGrainEffectsResolved(giverId, issue.StringId));
        }
    }

    private void SendReturnedCompanionStates(MessagePayload<AlternativeTroopsReturned> payload)
    {
        if (payload.What.Issue is not Issue issue) return;
        foreach (var troop in issue.AlternativeSolutionSentTroops.GetTroopRoster())
        {
            if (troop.Character.IsHero && troop.Character.HeroObject.HeroState == Hero.CharacterStates.Active)
            {
                var hero = troop.Character.HeroObject;
                broker.Publish(hero, new HeroStateChanged((int)hero.HeroState, hero));
            }
        }
    }

    private void HandleEffectsResolved(MessagePayload<NetworkHeadmanNeedsGrainEffectsResolved> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() => conversationGuard.RunInOrder(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.GiverId, out var giver)) return;
            var issue = giver.Issue;
            if (!(issue is TaleWorlds.CampaignSystem.Issues.HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssue) ||
                issue.StringId != data.IssueId || !issue.IsSolvingWithAlternative) return;
            issue._areIssueEffectsResolved = true;
        }, data.GiverId), context: nameof(HeadmanNeedsGrainJournalHandler));
    }
}
