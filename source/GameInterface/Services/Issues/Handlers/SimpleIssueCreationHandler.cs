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
/// Server-authoritative creation replication for every <see cref="SimpleIssueFactoryRegistry"/>-registered
/// issue type (<c>ArmyNeedsSuppliesIssue</c>, <c>LandlordTrainingForRetainersIssue</c>,
/// <c>GangLeaderNeedsRecruitsIssue</c>, <c>LadysKnightOutIssue</c>) - one shared handler instead of four
/// nearly-identical bespoke ones, since none of these types roll or reference a field a client needs
/// captured/forced to replicate byte-identically (see the registry's own doc comment). Each type's own
/// ACCEPT-time and completion-side handling (where it needs anything beyond the fully generic mirror - see
/// <c>GenericAcceptMirrorIssueTypes</c>) still lives in its own bespoke Patches/Handler file.
/// </summary>
internal class SimpleIssueCreationHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<SimpleIssueCreationHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public SimpleIssueCreationHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<SimpleIssueCreated>(Handle_SimpleIssueCreated);
        messageBroker.Subscribe<NetworkSimpleIssueCreated>(Handle_NetworkSimpleIssueCreated);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<SimpleIssueCreated>(Handle_SimpleIssueCreated);
        messageBroker.Unsubscribe<NetworkSimpleIssueCreated>(Handle_NetworkSimpleIssueCreated);
    }

    private void Handle_SimpleIssueCreated(MessagePayload<SimpleIssueCreated> payload)
    {
        if (ModInformation.IsClient) return;

        var issue = payload.What.Issue;
        if (issue?.IssueOwner == null) return;
        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var ownerId)) return;
        if (!SimpleIssueFactoryRegistry.TryGetKey(issue, out var key))
        {
            Logger.Error("SimpleIssueCreated published for an unregistered issue type {Type} for owner {Owner}", issue.GetType(), ownerId);
            return;
        }

        network.SendAll(new NetworkSimpleIssueCreated(ownerId, key));
    }

    private void Handle_NetworkSimpleIssueCreated(MessagePayload<NetworkSimpleIssueCreated> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;

            // Idempotent: a resend (or the full save transfer at join already having restored this via the
            // normal savegame) must not create a second issue for the same hero.
            if (owner.Issue != null) return;

            if (!SimpleIssueFactoryRegistry.TryConstructAndRegister(data.IssueKey, owner))
            {
                Logger.Error("Received NetworkSimpleIssueCreated with unknown IssueKey {Key} for owner {Owner}", data.IssueKey, data.OwnerId);
            }
        });
    }
}
