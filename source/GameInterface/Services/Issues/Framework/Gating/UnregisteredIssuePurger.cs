using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Issues.Framework.Interface;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Framework.Gating;

/// <summary>
/// Ran into an issue where generating a new campaign and then switching to hosting left issues
/// already generated. This removes any issues not registered. Also, this can probably be removed once all
/// quests are implemented.
/// </summary>
internal class UnregisteredIssuePurger : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<UnregisteredIssuePurger>();

    private readonly IMessageBroker messageBroker;
    private readonly IQuestTypeRegistry registry;
    private readonly IQuestFeatureFlag questFeatureFlag;
    private readonly IIssueDismisser issueDismisser;

    public UnregisteredIssuePurger(
        IMessageBroker messageBroker,
        IQuestTypeRegistry registry,
        IQuestFeatureFlag questFeatureFlag,
        IIssueDismisser issueDismisser)
    {
        this.messageBroker = messageBroker;
        this.registry = registry;
        this.questFeatureFlag = questFeatureFlag;
        this.issueDismisser = issueDismisser;

        messageBroker.Subscribe<CampaignReady>(Handle_CampaignReady);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<CampaignReady>(Handle_CampaignReady);
    }

    private void Handle_CampaignReady(MessagePayload<CampaignReady> payload)
    {
        if (ModInformation.IsClient)
        {
            return;
        }

        try
        {
            Purge();
        }
        catch (Exception e)
        {
            Logger.Error(e, "Purging unregistered issues failed");
        }
    }

    internal void Purge()
    {
        var issueManager = Campaign.Current?.IssueManager;
        if (issueManager == null)
        {
            return;
        }

        var unregistered = issueManager.Issues.Values
            .Where(issue => !registry.IsRegistered(issue.GetType()))
            .ToList();

        var removed = 0;
        var dismissed = 0;

        foreach (var issue in unregistered)
        {
            try
            {
                // A save from a quests-enabled build can hold taken issues that nothing syncs once quests are off
                if (!issue.IsOngoingWithoutQuest && !questFeatureFlag.IsEnabled)
                {
                    issueDismisser.Dismiss(issue);
                    dismissed++;
                    continue;
                }

                if (!issue.IsOngoingWithoutQuest)
                {
                    Logger.Warning(
                        "Keeping {issue} owned by {owner}, it is already being solved but its type is not registered",
                        issue.GetType().Name,
                        issue.IssueOwner?.StringId);
                    continue;
                }

                issue.IssueFinalized();
                removed++;
            }
            catch (Exception e)
            {
                Logger.Error(e, "Purging {issue} offered by {owner} failed", issue.GetType().Name, issue.IssueOwner?.StringId);
            }
        }

        Logger.Information("Removed {removed} issues of unregistered types", removed);

        if (dismissed > 0)
        {
            Logger.Information("Dismissed {dismissed} taken issues because quests are turned off", dismissed);
        }
    }
}
