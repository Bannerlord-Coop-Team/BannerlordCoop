using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Handlers;

/// <summary>
/// Server-authoritative Lord Needs Horses issue CREATION replication only - acceptance (quest-solution and
/// alternative-solution) rides the fully generic mirror (see <c>GenericAcceptMirrorIssueTypes</c>) unchanged,
/// and removal rides the existing generic <see cref="Patches.IssueFinalizedPatches"/>/
/// <see cref="VillageNeedsToolsIssueHandler"/> choke point (see
/// <see cref="VillageNeedsCraftingMaterialsIssueHandler"/>'s doc comment for why per-type handlers
/// deliberately don't add their own parallel finalize subscription).
/// </summary>
internal class LordNeedsHorsesIssueHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<LordNeedsHorsesIssueHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ILordNeedsHorsesIssueInterface issueInterface;

    public LordNeedsHorsesIssueHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ILordNeedsHorsesIssueInterface issueInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.issueInterface = issueInterface;

        messageBroker.Subscribe<LordNeedsHorsesIssueCreated>(Handle_LordNeedsHorsesIssueCreated);
        messageBroker.Subscribe<NetworkLordNeedsHorsesIssueCreated>(Handle_NetworkLordNeedsHorsesIssueCreated);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<LordNeedsHorsesIssueCreated>(Handle_LordNeedsHorsesIssueCreated);
        messageBroker.Unsubscribe<NetworkLordNeedsHorsesIssueCreated>(Handle_NetworkLordNeedsHorsesIssueCreated);
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

        network.SendAll(new NetworkLordNeedsHorsesIssueCreated(ownerId, mountItemId, numMounts, mountValuePerUnit));
    }

    private void Handle_NetworkLordNeedsHorsesIssueCreated(MessagePayload<NetworkLordNeedsHorsesIssueCreated> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            if (owner.Issue != null) return; // idempotent

            if (!objectManager.TryGetObjectWithLogging<ItemObject>(data.MountItemId, out var mountItem)) return;

            var replicated = issueInterface.ConstructReplicated(owner, mountItem, data.NumMountsToBeDelivered, data.MountValuePerUnit);

            issueInterface.RegisterReplicated(owner, replicated);
        });
    }
}
