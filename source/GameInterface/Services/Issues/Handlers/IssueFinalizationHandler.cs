using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
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
        messageBroker.Subscribe<QuestSuccessTriggered>(Handle_QuestSuccessTriggered);
        messageBroker.Subscribe<RequestIssueRemoved>(Handle_RequestIssueRemoved);
        messageBroker.Subscribe<NetworkIssueRemoved>(Handle_NetworkIssueRemoved);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<IssueFinalizedTriggered>(Handle_IssueFinalizedTriggered);
        messageBroker.Unsubscribe<QuestSuccessTriggered>(Handle_QuestSuccessTriggered);
        messageBroker.Unsubscribe<RequestIssueRemoved>(Handle_RequestIssueRemoved);
        messageBroker.Unsubscribe<NetworkIssueRemoved>(Handle_NetworkIssueRemoved);
    }

    private static byte CaptureProof(IssueBase issue, IssueFinalizeReason reason)
    {
        var descriptor = QuestTypeRegistry.Get(issue);
        return reason switch
        {
            IssueFinalizeReason.QuestSuccess => descriptor?.CaptureQuestSuccessProof?.Invoke(issue) ?? 0,
            IssueFinalizeReason.QuestFail => descriptor?.CaptureQuestFailProof?.Invoke(issue) ?? 0,
            IssueFinalizeReason.QuestBetrayal => descriptor?.CaptureQuestBetrayalProof?.Invoke(issue) ?? 0,
            _ => (byte)0,
        };
    }

    private static void SetProofContext(IssueFinalizeReason reason, byte proof)
    {
        switch (reason)
        {
            case IssueFinalizeReason.QuestSuccess:
                QuestSuccessProofContext.Set(proof);
                break;
            case IssueFinalizeReason.QuestFail:
                QuestFailProofContext.Set(proof);
                break;
            case IssueFinalizeReason.QuestBetrayal:
                QuestBetrayalProofContext.Set(proof);
                break;
        }
    }

    private static byte ReadProofContext(IssueFinalizeReason reason) => reason switch
    {
        IssueFinalizeReason.QuestSuccess => QuestSuccessProofContext.Current,
        IssueFinalizeReason.QuestFail => QuestFailProofContext.Current,
        IssueFinalizeReason.QuestBetrayal => QuestBetrayalProofContext.Current,
        _ => (byte)0,
    };

    private void Handle_IssueFinalizedTriggered(MessagePayload<IssueFinalizedTriggered> payload)
    {
        var owner = payload.What.Owner;
        var reason = payload.What.Reason;
        if (owner == null || !objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        if (ModInformation.IsServer)
        {
            var proof = IssueFinalizeAuthorityGuard.IsActive ? ReadProofContext(reason) : CaptureProof(owner.Issue, reason);
            network.SendAll(new NetworkIssueRemoved(ownerId, reason, proof));
        }
        else
        {
            generationRegistry.TryGetGeneration(owner, out var generation);
            var proof = CaptureProof(owner.Issue, reason);
            network.SendAll(new RequestIssueRemoved(ownerId, reason, generation, proof));
        }
    }

    private void Handle_QuestSuccessTriggered(MessagePayload<QuestSuccessTriggered> payload)
    {
        var owner = payload.What.Owner;
        if (owner == null || !objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        if (ModInformation.IsServer)
        {
            FinalizeHostQuestSuccess(owner, ownerId, payload.What.ControllerId);
        }
        else
        {
            generationRegistry.TryGetGeneration(owner, out var generation);
            var successProof = QuestTypeRegistry.Get(owner.Issue)?.CaptureQuestSuccessProof?.Invoke(owner.Issue) ?? 0;
            network.SendAll(new RequestIssueRemoved(ownerId, IssueFinalizeReason.QuestSuccess, generation, successProof));
        }
    }

    private void FinalizeHostQuestSuccess(Hero owner, string ownerId, string hostControllerId)
    {
        if (hostControllerId == null || !playerManager.TryGetPlayer(hostControllerId, out var player))
        {
            Logger.Error("Rejecting the host's own {Message} for owner {Owner} - could not resolve host's own Player",
                nameof(QuestSuccessTriggered), ownerId);
            return;
        }

        if (owner.Issue?.IssueQuest is not { IsOngoing: true })
        {
            Logger.Error("Rejecting the host's own {Message} for owner {Owner} - no ongoing quest to finalize",
                nameof(QuestSuccessTriggered), ownerId);
            return;
        }

        MobileParty hostParty = null;
        if (player.MobilePartyId != null)
        {
            objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out hostParty);
        }

        var descriptor = QuestTypeRegistry.Get(owner.Issue);
        var successProof = descriptor?.CaptureQuestSuccessProof?.Invoke(owner.Issue) ?? 0;
        var validator = descriptor?.ValidateQuestSuccess;
        if (validator != null)
        {
            QuestSuccessProofContext.Set(successProof);
            bool validated;
            try
            {
                validated = validator(owner.Issue, hostParty);
            }
            finally
            {
                QuestSuccessProofContext.Set(0);
            }

            if (!validated)
            {
                Logger.Error("Rejecting the host's own {Message} for owner {Owner} - completion condition not met for the host's real party",
                    nameof(QuestSuccessTriggered), ownerId);
                return;
            }
        }

        FinalizeAndBroadcast(owner, ownerId, player, IssueFinalizeReason.QuestSuccess, successProof);
    }

    private void FinalizeAndBroadcast(Hero owner, string ownerId, Player player, IssueFinalizeReason reason, byte proof = 0)
    {
        MobileParty ownerParty = null;
        if (player.MobilePartyId != null)
        {
            objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out ownerParty);
        }

        objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var truePlayerHero);

        SetProofContext(reason, proof);
        try
        {
            using (new MainHeroSubstitutionScope(truePlayerHero ?? owner, ownerParty))
            {
                IssueFinalizationSupport.FinalizeMirror(owner, reason, suppressReplicationPatches: false);
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to finalize {Reason} for owner {Owner} - not broadcasting", reason, ownerId);
        }
        finally
        {
            SetProofContext(reason, 0);
        }
    }

    private void Handle_RequestIssueRemoved(MessagePayload<RequestIssueRemoved> payload)
    {
        if (ModInformation.IsClient) return;

        var ownerId = payload.What.OwnerId;
        var reason = payload.What.Reason;
        var requestedGeneration = payload.What.Generation;
        var requester = payload.Who as NetPeer;

        if (!Enum.IsDefined(typeof(IssueFinalizeReason), reason))
        {
            Logger.Error("Rejecting {Message} claiming an undefined reason value ({Reason}) for owner {Owner}",
                nameof(RequestIssueRemoved), reason, ownerId);
            return;
        }

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;
            if (requester == null || !playerManager.TryGetPlayer(requester, out var player))
            {
                Logger.Error("Rejecting {Message} from an unregistered/unknown requester for owner {Owner}",
                    nameof(RequestIssueRemoved), ownerId);
                return;
            }

            if (!TryAuthorizeRequestIssueRemoved(owner, player, reason, requestedGeneration, ownerId)) return;
            if (!TryValidateFinalizeReason(owner, reason, player, payload.What.Proof, ownerId)) return;

            FinalizeAndBroadcast(owner, ownerId, player, reason, payload.What.Proof);
        });
    }

    private bool TryAuthorizeRequestIssueRemoved(Hero owner, Player player, IssueFinalizeReason reason, int requestedGeneration, string ownerId)
    {
        if (!ownershipRegistry.TryGetOwnerControllerId(owner, out var recordedOwner) || recordedOwner != player.ControllerId)
        {
            Logger.Error("Rejecting {Message} from {Requester}, who is not the recorded owner of {Owner}",
                nameof(RequestIssueRemoved), player.ControllerId, ownerId);
            return false;
        }

        if (!generationRegistry.TryGetGeneration(owner, out var currentGeneration) || currentGeneration != requestedGeneration)
        {
            Logger.Error("Rejecting {Message} for a stale/superseded issue generation for owner {Owner}",
                nameof(RequestIssueRemoved), ownerId);
            return false;
        }

        if (owner.Issue == null)
        {
            Logger.Error("Rejecting {Message} claiming {Reason} for owner {Owner} - no active issue to finalize",
                nameof(RequestIssueRemoved), reason, ownerId);
            return false;
        }

        if (reason == IssueFinalizeReason.RejectedAccept || reason == IssueFinalizeReason.AlternativeSolutionSuccess)
        {
            Logger.Error("Rejecting {Message} claiming {Reason} for owner {Owner} - this reason can only originate server-side",
                nameof(RequestIssueRemoved), reason, ownerId);
            return false;
        }

        return true;
    }

    private bool TryValidateFinalizeReason(Hero owner, IssueFinalizeReason reason, Player player, byte proof, string ownerId)
    {
        var quest = owner.Issue.IssueQuest is { IsOngoing: true } ongoingQuest ? ongoingQuest : null;

        switch (reason)
        {
            case IssueFinalizeReason.QuestSuccess:
                return ValidateQuestSuccessFinalize(owner, quest, player, proof, ownerId);
            case IssueFinalizeReason.QuestCancel:
            case IssueFinalizeReason.QuestFail:
            case IssueFinalizeReason.QuestTimeout:
            case IssueFinalizeReason.QuestBetrayal:
                return ValidateQuestTerminationFinalize(owner, reason, quest, proof, ownerId);
            case IssueFinalizeReason.IssueOnly:
                return ValidateIssueOnlyFinalize(owner, ownerId);
            default:
                if (quest == null) return true;

                Logger.Error("Rejecting {Message} claiming {Reason} for owner {Owner} - an ongoing quest exists and must be finalized with a quest-specific reason",
                    nameof(RequestIssueRemoved), reason, ownerId);
                return false;
        }
    }

    private bool ValidateQuestSuccessFinalize(Hero owner, QuestBase quest, Player player, byte proof, string ownerId)
    {
        if (quest == null)
        {
            Logger.Error("Rejecting {Message} claiming QuestSuccess for owner {Owner} - no ongoing quest to finalize",
                nameof(RequestIssueRemoved), ownerId);
            return false;
        }

        var validator = QuestTypeRegistry.Get(owner.Issue)?.ValidateQuestSuccess;
        if (validator == null)
        {
            Logger.Error("Rejecting {Message} claiming QuestSuccess for owner {Owner} - quest type has no registered ValidateQuestSuccess, completion cannot be re-derived server-side",
                nameof(RequestIssueRemoved), ownerId);
            return false;
        }

        MobileParty party = null;
        if (player.MobilePartyId != null)
        {
            objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out party);
        }

        QuestSuccessProofContext.Set(proof);
        bool validated;
        try
        {
            validated = validator(owner.Issue, party);
        }
        finally
        {
            QuestSuccessProofContext.Set(0);
        }

        if (!validated)
        {
            Logger.Error("Rejecting {Message} claiming QuestSuccess for owner {Owner} - completion condition not met for the requester's real party",
                nameof(RequestIssueRemoved), ownerId);
            return false;
        }

        return true;
    }

    private bool ValidateQuestTerminationFinalize(Hero owner, IssueFinalizeReason reason, QuestBase quest, byte proof, string ownerId)
    {
        if (quest == null)
        {
            Logger.Error("Rejecting {Message} claiming {Reason} for owner {Owner} - no ongoing quest to finalize",
                nameof(RequestIssueRemoved), reason, ownerId);
            return false;
        }

        if (reason == IssueFinalizeReason.QuestTimeout)
        {
            if (!quest.QuestDueTime.IsPast)
            {
                Logger.Error("Rejecting {Message} claiming {Reason} for owner {Owner} - due time has not passed",
                    nameof(RequestIssueRemoved), reason, ownerId);
                return false;
            }

            return true;
        }

        var descriptor = QuestTypeRegistry.Get(owner.Issue);
        var validator = reason switch
        {
            IssueFinalizeReason.QuestCancel => descriptor?.ValidateQuestCancel,
            IssueFinalizeReason.QuestBetrayal => descriptor?.ValidateQuestBetrayal,
            IssueFinalizeReason.QuestFail => descriptor?.ValidateQuestFail,
            _ => null,
        };

        SetProofContext(reason, proof);
        bool validated;
        try
        {
            validated = validator != null && validator(owner.Issue);
        }
        finally
        {
            SetProofContext(reason, 0);
        }

        if (!validated)
        {
            Logger.Error("Rejecting {Message} claiming {Reason} for owner {Owner} - quest type has no registered validator or condition not met",
                nameof(RequestIssueRemoved), reason, ownerId);
            return false;
        }

        return true;
    }

    private bool ValidateIssueOnlyFinalize(Hero owner, string ownerId)
    {
        if (!owner.Issue.IsOngoingWithoutQuest)
        {
            Logger.Error("Rejecting {Message} claiming IssueOnly for owner {Owner} - a solution is already in progress and must be finalized through its own completion path",
                nameof(RequestIssueRemoved), ownerId);
            return false;
        }

        return true;
    }

    private void Handle_NetworkIssueRemoved(MessagePayload<NetworkIssueRemoved> payload)
    {
        if (ModInformation.IsServer) return;

        var ownerId = payload.What.OwnerId;
        var reason = payload.What.Reason;
        var proof = payload.What.Proof;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;

            Hero truePlayerHero = null;
            MobileParty ownerParty = null;
            if (ownershipRegistry.TryGetOwnerControllerId(owner, out var recordedOwnerControllerId) &&
                playerManager.TryGetPlayer(recordedOwnerControllerId, out var player))
            {
                if (player.HeroId != null) objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out truePlayerHero);
                if (player.MobilePartyId != null) objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out ownerParty);
            }

            var isLocalPeerOwner = ownershipRegistry.IsLocalPeerOwner(owner);

            SetProofContext(reason, proof);
            try
            {
                using (new MainHeroSubstitutionScope(truePlayerHero ?? owner, ownerParty))
                {
                    IssueFinalizationSupport.FinalizeMirror(owner, reason, suppressReplicationPatches: true, skipConsequenceReapplication: isLocalPeerOwner);
                }
            }
            finally
            {
                SetProofContext(reason, 0);
            }
        });
    }
}
