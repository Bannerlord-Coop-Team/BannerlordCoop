using Common.Logging;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Registry;

internal class IssueResolver : IIssueResolver
{
    private static readonly ILogger Logger = LogManager.GetLogger<IssueResolver>();

    private readonly IObjectManager objectManager;
    private readonly IQuestTypeRegistry registry;

    public IssueResolver(IObjectManager objectManager, IQuestTypeRegistry registry)
    {
        this.objectManager = objectManager;
        this.registry = registry;
    }

    public bool TryResolve(
        string issueOwnerId,
        string issueId,
        out Hero issueOwner,
        out IssueBase issue,
        out IQuestTypeDescriptor descriptor)
    {
        issue = null;
        descriptor = null;

        if (!objectManager.TryGetObjectWithLogging(issueOwnerId, out issueOwner))
        {
            return false;
        }

        issue = issueOwner.Issue;

        if (issue == null || issue.StringId != issueId)
        {
            Logger.Warning("{hero} has no issue {issueId}", issueOwnerId, issueId);
            return false;
        }

        return registry.TryGet(issue.GetType(), out descriptor);
    }
}
