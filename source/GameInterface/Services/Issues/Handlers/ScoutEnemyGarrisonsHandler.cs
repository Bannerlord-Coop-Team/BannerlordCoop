using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Messages;

namespace GameInterface.Services.Issues.Handlers;

internal sealed class ScoutEnemyGarrisonsHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly IScoutEnemyGarrisonsService service;

    public ScoutEnemyGarrisonsHandler(IMessageBroker broker, IScoutEnemyGarrisonsService service)
    {
        this.broker = broker;
        this.service = service;
        broker.Subscribe<ScoutEnemyGarrisonsIssueChanged>(OnIssueChanged);
        broker.Subscribe<NetworkScoutEnemyGarrisonsIssue>(OnIssue);
        broker.Subscribe<NetworkScoutEnemyGarrisonsProgress>(OnProgress);
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

    public void Dispose()
    {
        broker.Unsubscribe<ScoutEnemyGarrisonsIssueChanged>(OnIssueChanged);
        broker.Unsubscribe<NetworkScoutEnemyGarrisonsIssue>(OnIssue);
        broker.Unsubscribe<NetworkScoutEnemyGarrisonsProgress>(OnProgress);
    }
}
