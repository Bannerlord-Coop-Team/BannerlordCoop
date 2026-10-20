using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using HarmonyLib;
using ProtoBuf;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;
using Issue = TaleWorlds.CampaignSystem.Issues.ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue;

namespace GameInterface.Services.Issues.Quests.ArtisanOverpricedGoods;

public readonly struct ArtisanJournalObserved : IEvent
{
    public readonly Issue Issue;
    public ArtisanJournalObserved(Issue issue) => Issue = issue;
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkArtisanJournal : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string GiverId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(3)] public readonly TextObject[] Logs;

    public NetworkArtisanJournal(string giverId, string issueId, TextObject[] logs)
    {
        GiverId = giverId;
        IssueId = issueId;
        Logs = logs;
    }
}

/// <summary>Sends the server's ending text before finalization removes the owning player's issue.</summary>
internal class ArtisanJournalHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly INetwork network;
    private readonly IObjectManager objects;
    private readonly IIssueResolver resolver;
    private readonly IIssueOwnerResolver owners;
    private readonly IControllerIdProvider controller;
    private readonly IPlayerManager players;

    public ArtisanJournalHandler(IMessageBroker broker, INetwork network, IObjectManager objects,
        IIssueResolver resolver, IIssueOwnerResolver owners, IControllerIdProvider controller,
        IPlayerManager players)
    {
        this.broker = broker;
        this.network = network;
        this.objects = objects;
        this.resolver = resolver;
        this.owners = owners;
        this.controller = controller;
        this.players = players;
        broker.Subscribe<ArtisanJournalObserved>(HandleObserved);
        broker.Subscribe<NetworkArtisanJournal>(HandleJournal);
    }

    public void Dispose()
    {
        broker.Unsubscribe<ArtisanJournalObserved>(HandleObserved);
        broker.Unsubscribe<NetworkArtisanJournal>(HandleJournal);
    }

    private void HandleObserved(MessagePayload<ArtisanJournalObserved> payload)
    {
        if (ModInformation.IsClient) return;
        var issue = payload.What.Issue;
        if (issue.IsSolvingWithLordSolution || !objects.TryGetIdWithLogging(issue.IssueOwner, out var giverId)) return;
        var entries = issue.IssueQuest?.JournalEntries ?? issue.JournalEntries;
        if (owners.TryGetOwnerControllerId(issue, out var controllerId) &&
            players.TryGetPeer(controllerId, out var peer))
            network.Send(peer, new NetworkArtisanJournal(giverId, issue.StringId, entries.Select(log => log.LogText).ToArray()));
    }

    private void HandleJournal(MessagePayload<NetworkArtisanJournal> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!resolver.TryResolve(data.GiverId, data.IssueId, out _, out var issue, out _) ||
                !owners.TryGetOwnerControllerId(issue, out var owner) || owner != controller.ControllerId) return;

            if (data.Logs == null) return;
            foreach (var text in data.Logs)
            {
                var entries = issue.IssueQuest?.JournalEntries ?? issue.JournalEntries;
                if (entries.Any(log => log.LogText.ToString() == text.ToString())) continue;
                if (issue.IssueQuest != null) issue.IssueQuest.AddLog(text);
                else issue.AddLog(new JournalLog(CampaignTime.Now, text));
            }
        }, context: nameof(ArtisanJournalHandler));
    }
}

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.IssueFinalized))]
internal class ArtisanJournalFinalizedPatch
{
    [HarmonyPrefix]
    private static void Prefix(IssueBase __instance)
    {
        if (ModInformation.IsServer && __instance is Issue issue)
            MessageBroker.Instance.Publish(issue, new ArtisanJournalObserved(issue));
    }
}
