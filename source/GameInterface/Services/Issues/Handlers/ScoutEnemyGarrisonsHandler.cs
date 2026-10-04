using Common;
using Common.Messaging;
using GameInterface.Services.Heroes.HeirSelection.Messages;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.UI.Messages;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Handlers;

internal sealed class ScoutEnemyGarrisonsHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly IScoutEnemyGarrisonsService service;
    private readonly IScoutEnemyGarrisonsQuestState state;

    public ScoutEnemyGarrisonsHandler(IMessageBroker broker, IScoutEnemyGarrisonsService service, IScoutEnemyGarrisonsQuestState state)
    {
        this.broker = broker;
        this.service = service;
        this.state = state;
        broker.Subscribe<ScoutEnemyGarrisonsIssueChanged>(OnIssueChanged);
        broker.Subscribe<NetworkScoutEnemyGarrisonsIssue>(OnIssue);
        broker.Subscribe<NetworkScoutEnemyGarrisonsProgress>(OnProgress);
        broker.Subscribe<NetworkQuestTypeAcceptRejected>(OnRejected);
        broker.Subscribe<SwitchedPlayer>(OnSwitchedPlayer);
        broker.Subscribe<PlayerHeirSelectionCompleted>(OnHeirSelected);
    }

    private void OnIssueChanged(MessagePayload<ScoutEnemyGarrisonsIssueChanged> payload)
    {
        if (ModInformation.IsServer) service.PublishIssue(payload.What.Issue, payload.What.Created);
    }

    private void OnIssue(MessagePayload<NetworkScoutEnemyGarrisonsIssue> payload)
    {
        if (ModInformation.IsClient) GameThread.RunSafe(() => service.ApplyIssue(payload.What));
    }

    private void OnProgress(MessagePayload<NetworkScoutEnemyGarrisonsProgress> payload)
    {
        if (ModInformation.IsClient) GameThread.RunSafe(() => service.ApplyProgress(payload.What));
    }

    private void OnRejected(MessagePayload<NetworkQuestTypeAcceptRejected> payload)
    {
        if (ModInformation.IsClient && !payload.What.IsAlternative)
            GameThread.RunSafe(() => service.RejectAcceptance(payload.What.OwnerId));
    }

    private void OnSwitchedPlayer(MessagePayload<SwitchedPlayer> payload)
    {
        if (ModInformation.IsClient)
            state.RestoreClientTracking(Campaign.Current.QuestManager, Campaign.Current.VisualTrackerManager);
    }

    private void OnHeirSelected(MessagePayload<PlayerHeirSelectionCompleted> payload)
    {
        if (ModInformation.IsServer) service.CancelReplacedHeroQuests(payload.What.PlayerHero);
    }

    public void Dispose()
    {
        broker.Unsubscribe<ScoutEnemyGarrisonsIssueChanged>(OnIssueChanged);
        broker.Unsubscribe<NetworkScoutEnemyGarrisonsIssue>(OnIssue);
        broker.Unsubscribe<NetworkScoutEnemyGarrisonsProgress>(OnProgress);
        broker.Unsubscribe<NetworkQuestTypeAcceptRejected>(OnRejected);
        broker.Unsubscribe<SwitchedPlayer>(OnSwitchedPlayer);
        broker.Unsubscribe<PlayerHeirSelectionCompleted>(OnHeirSelected);
    }
}
