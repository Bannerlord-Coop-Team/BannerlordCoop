using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Handlers;

internal class IssueFinalizationHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<IssueFinalizationHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly IIssueOwnershipRegistry ownershipRegistry;
    private readonly IIssueGenerationRegistry generationRegistry;

    public IssueFinalizationHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager,
        IIssueOwnershipRegistry ownershipRegistry,
        IIssueGenerationRegistry generationRegistry)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;
        this.ownershipRegistry = ownershipRegistry;
        this.generationRegistry = generationRegistry;

        messageBroker.Subscribe<IssueFinalizedTriggered>(Handle_IssueFinalizedTriggered);
        messageBroker.Subscribe<RequestIssueRemoved>(Handle_RequestIssueRemoved);
        messageBroker.Subscribe<NetworkIssueRemoved>(Handle_NetworkIssueRemoved);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<IssueFinalizedTriggered>(Handle_IssueFinalizedTriggered);
        messageBroker.Unsubscribe<RequestIssueRemoved>(Handle_RequestIssueRemoved);
        messageBroker.Unsubscribe<NetworkIssueRemoved>(Handle_NetworkIssueRemoved);
    }

    private void Handle_IssueFinalizedTriggered(MessagePayload<IssueFinalizedTriggered> payload)
    {
        var owner = payload.What.Owner;
        var reason = payload.What.Reason;
        if (owner == null || !objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        if (ModInformation.IsServer)
        {
            network.SendAll(new NetworkIssueRemoved(ownerId, reason));
        }
        else
        {
            generationRegistry.TryGetGeneration(owner, out var generation);
            network.SendAll(new RequestIssueRemoved(ownerId, reason, generation));
        }
    }

    private void Handle_RequestIssueRemoved(MessagePayload<RequestIssueRemoved> payload)
    {
        if (ModInformation.IsClient) return;

        var ownerId = payload.What.OwnerId;
        var reason = payload.What.Reason;
        var requestedGeneration = payload.What.Generation;
        var requester = payload.Who as NetPeer;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;

            if (requester == null || !playerManager.TryGetPlayer(requester, out var player))
            {
                Logger.Error("Rejecting {Message} from an unregistered/unknown requester for owner {Owner}",
                    nameof(RequestIssueRemoved), ownerId);
                return;
            }

            if (!ownershipRegistry.TryGetOwnerControllerId(owner, out var recordedOwner) || recordedOwner != player.ControllerId)
            {
                Logger.Error("Rejecting {Message} from {Requester}, who is not the recorded owner of {Owner}",
                    nameof(RequestIssueRemoved), player.ControllerId, ownerId);
                return;
            }

            if (!generationRegistry.TryGetGeneration(owner, out var currentGeneration) || currentGeneration != requestedGeneration)
            {
                Logger.Error("Rejecting {Message} for a stale/superseded issue generation for owner {Owner}",
                    nameof(RequestIssueRemoved), ownerId);
                return;
            }

            if (owner.Issue == null)
            {
                Logger.Error("Rejecting {Message} claiming {Reason} for owner {Owner} - no active issue to finalize",
                    nameof(RequestIssueRemoved), reason, ownerId);
                return;
            }

            if (reason == IssueFinalizeReason.RejectedAccept || reason == IssueFinalizeReason.AlternativeSolutionSuccess)
            {
                Logger.Error("Rejecting {Message} claiming {Reason} for owner {Owner} - this reason can only originate server-side",
                    nameof(RequestIssueRemoved), reason, ownerId);
                return;
            }

            var quest = owner.Issue.IssueQuest is { IsOngoing: true } ongoingQuest ? ongoingQuest : null;

            if (reason == IssueFinalizeReason.QuestSuccess)
            {
                var validator = QuestTypeRegistry.Get(owner.Issue)?.ValidateQuestSuccess;
                if (validator != null)
                {
                    MobileParty party = null;
                    if (player.MobilePartyId != null)
                    {
                        objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out party);
                    }

                    if (!validator(owner.Issue, party))
                    {
                        Logger.Error("Rejecting {Message} claiming QuestSuccess for owner {Owner} - completion condition not met for the requester's real party",
                            nameof(RequestIssueRemoved), ownerId);
                        return;
                    }
                }
            }
            else if (reason is IssueFinalizeReason.QuestCancel or IssueFinalizeReason.QuestFail
                or IssueFinalizeReason.QuestTimeout or IssueFinalizeReason.QuestBetrayal)
            {
                if (quest == null)
                {
                    Logger.Error("Rejecting {Message} claiming {Reason} for owner {Owner} - no ongoing quest to finalize",
                        nameof(RequestIssueRemoved), reason, ownerId);
                    return;
                }

                if (reason == IssueFinalizeReason.QuestTimeout && !quest.QuestDueTime.IsPast)
                {
                    Logger.Error("Rejecting {Message} claiming QuestTimeout for owner {Owner} - due time has not passed",
                        nameof(RequestIssueRemoved), ownerId);
                    return;
                }
            }
            else if (quest != null)
            {
                Logger.Error("Rejecting {Message} claiming {Reason} for owner {Owner} - an ongoing quest exists and must be finalized with a quest-specific reason",
                    nameof(RequestIssueRemoved), reason, ownerId);
                return;
            }

            MobileParty ownerParty = null;
            if (player.MobilePartyId != null)
            {
                objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out ownerParty);
            }

            objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var truePlayerHero);

            try
            {
                using (new MainHeroSubstitutionScope(truePlayerHero, ownerParty))
                {
                    IssueFinalizationSupport.FinalizeMirror(owner, reason);
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to finalize {Message} claiming {Reason} for owner {Owner} - not broadcasting",
                    nameof(RequestIssueRemoved), reason, ownerId);
                return;
            }

            network.SendAll(new NetworkIssueRemoved(ownerId, reason));
        });
    }

    private void Handle_NetworkIssueRemoved(MessagePayload<NetworkIssueRemoved> payload)
    {
        var ownerId = payload.What.OwnerId;
        var reason = payload.What.Reason;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;
            IssueFinalizationSupport.FinalizeMirror(owner, reason);
        });
    }
}
