using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Handlers;

/// <summary>
/// Server-authoritative Lord Needs a Tutor issue replication - creation-time young-hero capture (same shape as
/// <see cref="ArtisanOverpricedGoodsIssueHandler"/>'s creation half), plus the bespoke shadow-table relay for
/// the two never-reset booleans (see <see cref="Interfaces.LordsNeedsTutorQuestFlags"/>'s type doc comment for
/// the full derivation).
///
/// Deliberately does NOT subscribe to any accept-quest/finalize message of its own - this issue type IS in
/// <see cref="GenericAcceptMirrorIssueTypes.QuestSolutionMirrorEligible"/> (see
/// <see cref="Interfaces.ILordsNeedsTutorIssueInterface"/>'s type doc comment for why a bare replay is safe
/// here), so <see cref="Patches.IssueAcceptancePatches"/>/<see cref="Patches.IssueFinalizedPatches"/> already
/// cover accept/finalize entirely unchanged. This type has no alternative solution
/// (<c>IsThereAlternativeSolution == false</c>), so there is no alternative-solution-accept subscription pair
/// either.
/// </summary>
internal class LordsNeedsTutorIssueHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<LordsNeedsTutorIssueHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ILordsNeedsTutorIssueInterface issueInterface;

    public LordsNeedsTutorIssueHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ILordsNeedsTutorIssueInterface issueInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.issueInterface = issueInterface;

        messageBroker.Subscribe<LordsNeedsTutorIssueCreated>(Handle_LordsNeedsTutorIssueCreated);
        messageBroker.Subscribe<NetworkLordsNeedsTutorIssueCreated>(Handle_NetworkLordsNeedsTutorIssueCreated);

        messageBroker.Subscribe<LordsNeedsTutorQuestFlagTriggered>(Handle_LordsNeedsTutorQuestFlagTriggered);
        messageBroker.Subscribe<RequestLordsNeedsTutorQuestFlagSet>(Handle_RequestLordsNeedsTutorQuestFlagSet);
        messageBroker.Subscribe<NetworkLordsNeedsTutorQuestFlagSet>(Handle_NetworkLordsNeedsTutorQuestFlagSet);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<LordsNeedsTutorIssueCreated>(Handle_LordsNeedsTutorIssueCreated);
        messageBroker.Unsubscribe<NetworkLordsNeedsTutorIssueCreated>(Handle_NetworkLordsNeedsTutorIssueCreated);

        messageBroker.Unsubscribe<LordsNeedsTutorQuestFlagTriggered>(Handle_LordsNeedsTutorQuestFlagTriggered);
        messageBroker.Unsubscribe<RequestLordsNeedsTutorQuestFlagSet>(Handle_RequestLordsNeedsTutorQuestFlagSet);
        messageBroker.Unsubscribe<NetworkLordsNeedsTutorQuestFlagSet>(Handle_NetworkLordsNeedsTutorQuestFlagSet);
    }

    // --- Creation: server rolls once, broadcasts the resolved young hero ---

    private void Handle_LordsNeedsTutorIssueCreated(MessagePayload<LordsNeedsTutorIssueCreated> payload)
    {
        if (ModInformation.IsClient) return;

        var issue = payload.What.Issue;
        if (issue?.IssueOwner == null) return;
        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var ownerId)) return;

        if (!issueInterface.TryCaptureFields(issue, out var youngHero))
        {
            Logger.Error("Could not capture Lord Needs a Tutor issue fields for owner {Owner}", ownerId);
            return;
        }

        if (!objectManager.TryGetIdWithLogging(youngHero, out var youngHeroId)) return;

        network.SendAll(new NetworkLordsNeedsTutorIssueCreated(ownerId, youngHeroId));
    }

    private void Handle_NetworkLordsNeedsTutorIssueCreated(MessagePayload<NetworkLordsNeedsTutorIssueCreated> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;

            // Idempotent: a resend (or the full save transfer at join already having restored this via the
            // normal savegame) must not create a second issue for the same hero.
            if (owner.Issue != null) return;

            if (!objectManager.TryGetObjectWithLogging<Hero>(data.YoungHeroId, out var youngHero)) return;

            var replicated = issueInterface.ConstructReplicated(owner, youngHero);

            issueInterface.RegisterReplicated(owner, replicated);
        });
    }

    // --- Quest flag shadow-table relay (the task's core bug) ---

    private void Handle_LordsNeedsTutorQuestFlagTriggered(MessagePayload<LordsNeedsTutorQuestFlagTriggered> payload)
    {
        var owner = payload.What.Owner;
        if (owner == null || !objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        var flag = payload.What.Flag;
        if (ModInformation.IsServer)
        {
            // The host's own genuine transition - already applied to the registry by the capture patch itself;
            // broadcast it to every other connected peer.
            network.SendAll(new NetworkLordsNeedsTutorQuestFlagSet(ownerId, flag));
        }
        else
        {
            // A remote client's own genuine transition - a client's SendAll only reaches its one connection,
            // the server, which re-broadcasts to everyone (including back to this same client, for a uniform
            // idempotent apply path).
            network.SendAll(new RequestLordsNeedsTutorQuestFlagSet(ownerId, flag));
        }
    }

    private void Handle_RequestLordsNeedsTutorQuestFlagSet(MessagePayload<RequestLordsNeedsTutorQuestFlagSet> payload)
    {
        if (ModInformation.IsClient) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out _)) return;

            network.SendAll(new NetworkLordsNeedsTutorQuestFlagSet(data.OwnerId, data.Flag));
        });
    }

    private void Handle_NetworkLordsNeedsTutorQuestFlagSet(MessagePayload<NetworkLordsNeedsTutorQuestFlagSet> payload)
    {
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;

            LordsNeedsTutorQuestFlags.SetFlag(owner, data.Flag);

            // If this peer already has a live Quest object for this hero (the originating owner re-confirming
            // its own already-true field, or any other peer's own mirrored copy), force-write the corresponding
            // private field onto it directly too - covers the live, same-session case without waiting for a
            // later InitializeQuestOnGameLoad rehydrate (see LordsNeedsTutorQuestFlagsPersistencePatches).
            if (owner.Issue?.IssueQuest is LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest quest)
            {
                switch (data.Flag)
                {
                    case LordsNeedsTutorQuestFlag.DoNotForceYoungHeroOutFromClan:
                        quest._doNotForceYoungHeroOutFromClan = true;
                        break;
                    case LordsNeedsTutorQuestFlag.ShowQuestFailedConversation:
                        quest._showQuestFailedConversation = true;
                        break;
                }
            }
        });
    }
}
