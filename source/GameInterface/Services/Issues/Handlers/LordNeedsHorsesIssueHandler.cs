using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.UI.Messages;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Handlers;

using Issue = LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue;

internal class LordNeedsHorsesIssueHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<LordNeedsHorsesIssueHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ILordNeedsHorsesIssueInterface issueInterface;
    private readonly IIssueGenerationRegistry generations;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly ILordNeedsHorsesQuest questService;

    public LordNeedsHorsesIssueHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ILordNeedsHorsesIssueInterface issueInterface,
        IIssueGenerationRegistry generations,
        IIssueOwnershipRegistry ownership,
        ILordNeedsHorsesQuest questService)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.issueInterface = issueInterface;
        this.generations = generations;
        this.ownership = ownership;
        this.questService = questService;

        messageBroker.Subscribe<SwitchedPlayer>(Handle_SwitchedPlayer);
        messageBroker.Subscribe<NetworkQuestTypeAcceptRejected>(Handle_AcceptRejected);
        messageBroker.Subscribe<LordNeedsHorsesIssueCreated>(Handle_LordNeedsHorsesIssueCreated);
        messageBroker.Subscribe<NetworkLordNeedsHorsesIssueCreated>(Handle_NetworkLordNeedsHorsesIssueCreated);
        messageBroker.Subscribe<LordNeedsHorsesJournalChanged>(Handle_JournalChanged);
        messageBroker.Subscribe<NetworkLordNeedsHorsesJournal>(Handle_NetworkJournal);
        messageBroker.Subscribe<LordNeedsHorsesTraitProgressChanged>(Handle_TraitProgressChanged);
        messageBroker.Subscribe<NetworkLordNeedsHorsesTraitProgress>(Handle_NetworkTraitProgress);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<SwitchedPlayer>(Handle_SwitchedPlayer);
        messageBroker.Unsubscribe<NetworkQuestTypeAcceptRejected>(Handle_AcceptRejected);
        messageBroker.Unsubscribe<LordNeedsHorsesIssueCreated>(Handle_LordNeedsHorsesIssueCreated);
        messageBroker.Unsubscribe<NetworkLordNeedsHorsesIssueCreated>(Handle_NetworkLordNeedsHorsesIssueCreated);
        messageBroker.Unsubscribe<LordNeedsHorsesJournalChanged>(Handle_JournalChanged);
        messageBroker.Unsubscribe<NetworkLordNeedsHorsesJournal>(Handle_NetworkJournal);
        messageBroker.Unsubscribe<LordNeedsHorsesTraitProgressChanged>(Handle_TraitProgressChanged);
        messageBroker.Unsubscribe<NetworkLordNeedsHorsesTraitProgress>(Handle_NetworkTraitProgress);
    }

    private void Handle_SwitchedPlayer(MessagePayload<SwitchedPlayer> payload) => questService.RestoreLocalTraitProgress();

    private void Handle_AcceptRejected(MessagePayload<NetworkQuestTypeAcceptRejected> payload)
    {
        if (ModInformation.IsServer || payload.What.IsAlternative) return;
        GameThread.RunSafe(() =>
        {
            if (objectManager.TryGetObjectWithLogging<Hero>(payload.What.OwnerId, out var owner))
                questService.ResumeAcceptanceConversation(owner, accepted: false);
        });
    }

    private void Handle_LordNeedsHorsesIssueCreated(MessagePayload<LordNeedsHorsesIssueCreated> payload)
    {
        if (ModInformation.IsClient) return;

        var issue = payload.What.Issue;
        if (issue?.IssueOwner == null) return;
        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var ownerId)) return;

        if (!issueInterface.TryCaptureFields(issue, out var mountItem, out var numMounts, out var mountValuePerUnit))
        {
            Logger.Error("Could not capture Lord Needs Horses issue fields for owner {Owner}", ownerId);
            return;
        }

        if (!objectManager.TryGetIdWithLogging(mountItem, out var mountItemId)) return;

        issue._issueDifficultyMultiplier = Campaign.Current.Models.IssueModel.GetIssueDifficultyMultiplier();
        network.SendAll(new NetworkLordNeedsHorsesIssueCreated(ownerId, mountItemId, numMounts, mountValuePerUnit,
            generations.Bump(issue.IssueOwner), issue.IssueDueTime, issue.StringId, Campaign.Current.IssueManager._nextIssueUniqueIndex,
            issue._issueDifficultyMultiplier));
    }

    private void Handle_NetworkLordNeedsHorsesIssueCreated(MessagePayload<NetworkLordNeedsHorsesIssueCreated> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            if (generations.TryGetGeneration(owner, out var current) && current >= data.Generation) return;
            if (owner.Issue != null) return;

            if (!objectManager.TryGetObjectWithLogging<ItemObject>(data.MountItemId, out var mountItem)) return;

            using (new AllowedThread())
            {
                var replicated = issueInterface.ConstructReplicated(owner, mountItem, data.NumMountsToBeDelivered, data.MountValuePerUnit);
                replicated._issueDifficultyMultiplier = data.DifficultyMultiplier;
                replicated.IssueDueTime = data.DueTime;
                issueInterface.RegisterReplicated(owner, replicated);
                replicated.StringId = data.IssueId;
                Campaign.Current.IssueManager._nextIssueUniqueIndex = Math.Max(Campaign.Current.IssueManager._nextIssueUniqueIndex, data.NextIssueIndex);
                generations.SetGeneration(owner, data.Generation);
            }
        });
    }

    private void Handle_JournalChanged(MessagePayload<LordNeedsHorsesJournalChanged> payload)
    {
        if (ModInformation.IsClient) return;
        var issue = payload.What.Issue;
        if (!ownership.TryGetOwnerControllerId(issue.IssueOwner, out _)) return;
        network.SendAll(questService.CaptureJournal(issue, payload.What.Status));
    }

    private void Handle_TraitProgressChanged(MessagePayload<LordNeedsHorsesTraitProgressChanged> payload)
    {
        if (ModInformation.IsClient || !objectManager.TryGetIdWithLogging(payload.What.Hero, out var heroId)) return;
        network.SendAll(new NetworkLordNeedsHorsesTraitProgress(heroId, payload.What.Progress));
    }

    private void Handle_NetworkTraitProgress(MessagePayload<NetworkLordNeedsHorsesTraitProgress> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (objectManager.TryGetObjectWithLogging<Hero>(data.HeroId, out var hero))
                questService.ApplyTraitProgress(hero, data.Progress);
        });
    }

    private void Handle_NetworkJournal(MessagePayload<NetworkLordNeedsHorsesJournal> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner) ||
                owner.Issue is not Issue issue ||
                !generations.TryGetGeneration(owner, out var generation) || generation != data.Generation) return;
            questService.ApplyJournal(issue, data);
        });
    }
}
