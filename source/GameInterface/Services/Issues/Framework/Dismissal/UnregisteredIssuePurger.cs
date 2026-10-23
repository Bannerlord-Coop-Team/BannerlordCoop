using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Issues.Framework.Interface;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Framework.Dismissal;

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

    /// <summary>Subscribes the purge to campaign readiness.</summary>
    public UnregisteredIssuePurger(IMessageBroker messageBroker, IQuestTypeRegistry registry)
    {
        this.messageBroker = messageBroker;
        this.registry = registry;

        messageBroker.Subscribe<CampaignReady>(Handle_CampaignReady);
    }

    /// <summary>Unsubscribes from campaign readiness.</summary>
    public void Dispose()
    {
        messageBroker.Unsubscribe<CampaignReady>(Handle_CampaignReady);
    }

    /// <summary>Purges unregistered issues on the server once the campaign is ready.</summary>
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

    /// <summary>Finalizes every unregistered issue that nobody is solving yet.</summary>
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

        foreach (var issue in unregistered)
        {
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

        Logger.Information("Removed {removed} issues of unregistered types", removed);
    }
}
