using Common.Messaging;
using Common.Network;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Issues.Framework.Dismissal;
using GameInterface.Services.Issues.Framework.Visibility;
using GameInterface.Services.ObjectManager;
using System.Linq;
using TaleWorlds.CampaignSystem;
using Issue = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssue;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsGrain;

/// <summary>Server: sends the issue's resolved effects after each daily tick and the companions' state once the troops are back.</summary>
internal class HeadmanNeedsGrainServerSyncHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public HeadmanNeedsGrainServerSyncHandler(IMessageBroker broker, IObjectManager objectManager, INetwork network)
    {
        this.broker = broker;
        this.objectManager = objectManager;
        this.network = network;
        broker.Subscribe<IssueManagerDailyTicked>(SendResolvedEffects);
        broker.Subscribe<AlternativeTroopsReturned>(SendReturnedCompanionStates);
    }

    public void Dispose()
    {
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
}
