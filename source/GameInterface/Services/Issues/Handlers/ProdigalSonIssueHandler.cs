using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Handlers;

/// <summary>
/// Server-authoritative Prodigal Son issue CREATION replication only - acceptance (both quest-solution and
/// alternative-solution) rides the fully generic mirror (see <c>GenericAcceptMirrorIssueTypes</c>) unchanged,
/// and removal rides the existing generic finalize choke point (see
/// <see cref="VillageNeedsCraftingMaterialsIssueHandler"/>'s doc comment for why per-type handlers deliberately
/// don't add their own parallel finalize subscription).
/// </summary>
internal class ProdigalSonIssueHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ProdigalSonIssueHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IProdigalSonIssueInterface issueInterface;

    public ProdigalSonIssueHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IProdigalSonIssueInterface issueInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.issueInterface = issueInterface;

        messageBroker.Subscribe<ProdigalSonIssueCreated>(Handle_ProdigalSonIssueCreated);
        messageBroker.Subscribe<NetworkProdigalSonIssueCreated>(Handle_NetworkProdigalSonIssueCreated);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ProdigalSonIssueCreated>(Handle_ProdigalSonIssueCreated);
        messageBroker.Unsubscribe<NetworkProdigalSonIssueCreated>(Handle_NetworkProdigalSonIssueCreated);
    }

    private void Handle_ProdigalSonIssueCreated(MessagePayload<ProdigalSonIssueCreated> payload)
    {
        if (ModInformation.IsClient) return;

        var issue = payload.What.Issue;
        if (issue?.IssueOwner == null) return;
        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var ownerId)) return;

        if (!issueInterface.TryCaptureFields(issue, out var prodigalSon, out var targetHero))
        {
            Logger.Error("Could not capture Prodigal Son issue fields for owner {Owner}", ownerId);
            return;
        }

        if (!objectManager.TryGetIdWithLogging(prodigalSon, out var prodigalSonId)) return;
        if (!objectManager.TryGetIdWithLogging(targetHero, out var targetHeroId)) return;

        network.SendAll(new NetworkProdigalSonIssueCreated(ownerId, prodigalSonId, targetHeroId));
    }

    private void Handle_NetworkProdigalSonIssueCreated(MessagePayload<NetworkProdigalSonIssueCreated> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            if (owner.Issue != null) return; // idempotent

            if (!objectManager.TryGetObjectWithLogging<Hero>(data.ProdigalSonId, out var prodigalSon)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.TargetHeroId, out var targetHero)) return;

            var replicated = issueInterface.ConstructReplicated(owner, prodigalSon, targetHero);

            issueInterface.RegisterReplicated(owner, replicated);
        });
    }
}
