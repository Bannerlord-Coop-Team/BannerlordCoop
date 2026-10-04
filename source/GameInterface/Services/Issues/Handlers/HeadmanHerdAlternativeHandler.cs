using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Handlers;

using Issue = HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue;

internal sealed class HeadmanHerdAlternativeHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly INetwork network;
    private readonly IObjectManager objects;
    private readonly IIssueGenerationRegistry generations;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IHeadmanHerdQuestAuthority authority;

    public HeadmanHerdAlternativeHandler(IMessageBroker broker, INetwork network, IObjectManager objects,
        IIssueGenerationRegistry generations, IIssueOwnershipRegistry ownership, IHeadmanHerdQuestAuthority authority)
    {
        this.broker = broker;
        this.network = network;
        this.objects = objects;
        this.generations = generations;
        this.ownership = ownership;
        this.authority = authority;
        broker.Subscribe<HeadmanHerdAlternativeUpdated>(HandleUpdated);
        broker.Subscribe<NetworkHeadmanHerdAlternativeUpdated>(HandleReceived);
    }

    public void Dispose()
    {
        broker.Unsubscribe<HeadmanHerdAlternativeUpdated>(HandleUpdated);
        broker.Unsubscribe<NetworkHeadmanHerdAlternativeUpdated>(HandleReceived);
    }

    private void HandleUpdated(MessagePayload<HeadmanHerdAlternativeUpdated> payload)
    {
        if (ModInformation.IsClient) return;
        var issue = payload.What.Issue;
        if (!ownership.TryGetOwnerControllerId(issue.IssueOwner, out _)) return;
        if (!generations.TryGetGeneration(issue.IssueOwner, out var generation)) return;
        if (!objects.TryGetIdWithLogging(issue.IssueOwner, out var ownerId)) return;
        network.SendAll(new NetworkHeadmanHerdAlternativeUpdated(ownerId, generation, issue.StringId,
            issue.JournalEntries.Select(log => new HeadmanHerdJournalEntryData(log)).ToArray(),
            issue._areIssueEffectsResolved, payload.What.Detail));
    }

    private void HandleReceived(MessagePayload<NetworkHeadmanHerdAlternativeUpdated> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objects.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            if (!generations.TryGetGeneration(owner, out var generation) || generation != data.Generation) return;
            if (owner.Issue is not Issue issue || issue.StringId != data.IssueId || !issue.IsSolvingWithAlternative) return;
            if (data.Entries == null || data.Entries.Length < issue.JournalEntries.Count) return;
            if (!authority.TryEnter(owner, out var ownerScope)) return;
            using (ownerScope)
            using (new AllowedThread())
            {
                issue._areIssueEffectsResolved = data.EffectsResolved;
                for (var i = 0; i < data.Entries.Length; i++)
                {
                    if (i < issue.JournalEntries.Count)
                        issue.JournalEntries[i].UpdateCurrentProgress(data.Entries[i].Progress);
                    else
                        issue.AddLog(data.Entries[i].ToLog());
                }
                if (data.Detail != IssueBase.IssueUpdateDetails.None)
                    CampaignEventDispatcher.Instance.OnIssueUpdated(issue, data.Detail, Hero.MainHero);
            }
        });
    }
}
