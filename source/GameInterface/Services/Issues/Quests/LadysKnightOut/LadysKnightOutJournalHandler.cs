using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Quests.LadysKnightOut;

internal sealed class LadysKnightOutJournalHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly INetwork network;
    private readonly IObjectManager objects;
    private readonly IIssueOwnerResolver owners;
    private readonly IIssueResolver issues;
    private readonly IControllerIdProvider controller;
    private readonly IPlayerManager players;

    public LadysKnightOutJournalHandler(IMessageBroker broker, INetwork network, IObjectManager objects,
        IIssueOwnerResolver owners, IIssueResolver issues, IControllerIdProvider controller, IPlayerManager players)
    {
        this.broker = broker;
        this.network = network;
        this.objects = objects;
        this.owners = owners;
        this.issues = issues;
        this.controller = controller;
        this.players = players;
        broker.Subscribe<LadysKnightOutIssueLogAdded>(HandleIssueAdded);
        broker.Subscribe<NetworkLadysKnightOutLog>(HandleLog);
    }

    public void Dispose()
    {
        broker.Unsubscribe<LadysKnightOutIssueLogAdded>(HandleIssueAdded);
        broker.Unsubscribe<NetworkLadysKnightOutLog>(HandleLog);
    }

    private void HandleIssueAdded(MessagePayload<LadysKnightOutIssueLogAdded> payload)
    {
        var issue = payload.What.Issue;
        if (ModInformation.IsClient || !owners.TryGetOwnerControllerId(issue, out var owner) ||
            !objects.TryGetIdWithLogging(issue.IssueOwner, out var giverId) || !players.TryGetPeer(owner, out var peer)) return;
        network.Send(peer, new NetworkLadysKnightOutLog(
            giverId,
            issue.StringId,
            owner,
            issue.JournalEntries[issue.JournalEntries.Count - 1].LogText.ToString()));
    }

    private void HandleLog(MessagePayload<NetworkLadysKnightOutLog> payload)
    {
        if (ModInformation.IsServer) return;
        GameThread.RunSafe(() =>
        {
            var data = payload.What;
            if (data.ControllerId != controller.ControllerId ||
                !issues.TryResolve(data.GiverId, data.IssueId, out _, out var issue, out _)) return;
            using (new AllowedThread())
            {
                issue.AddLog(new JournalLog(CampaignTime.Now, new TextObject(data.Text)));
            }
        }, context: nameof(LadysKnightOutJournalHandler));
    }
}
