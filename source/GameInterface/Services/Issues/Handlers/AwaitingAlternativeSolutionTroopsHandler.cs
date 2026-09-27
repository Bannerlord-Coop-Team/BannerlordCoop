using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Party;
using GameInterface.Services.Players;
using GameInterface.Services.TroopRosters.Data;
using GameInterface.Services.TroopRosters.Interfaces;
using LiteNetLib;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Issues.Handlers;

internal class AwaitingAlternativeSolutionTroopsHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<AwaitingAlternativeSolutionTroopsHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly ITroopRosterInterface troopRosterInterface;
    private readonly IPlayerManager playerManager;
    private readonly IIssueOwnershipRegistry ownershipRegistry;
    private readonly IIssueGenerationRegistry generationRegistry;
    private readonly IAwaitingAlternativeSolutionTroopsRegistry troopsRegistry;
    private readonly IPrisonerSaleValidator troopValidator;
    private readonly Dictionary<string, int> depositedGenerationByOwnerId = new();

    public AwaitingAlternativeSolutionTroopsHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        ITroopRosterInterface troopRosterInterface,
        IPlayerManager playerManager,
        IIssueOwnershipRegistry ownershipRegistry,
        IIssueGenerationRegistry generationRegistry,
        IAwaitingAlternativeSolutionTroopsRegistry troopsRegistry,
        IPrisonerSaleValidator troopValidator)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.troopRosterInterface = troopRosterInterface;
        this.playerManager = playerManager;
        this.ownershipRegistry = ownershipRegistry;
        this.generationRegistry = generationRegistry;
        this.troopsRegistry = troopsRegistry;
        this.troopValidator = troopValidator;

        messageBroker.Subscribe<AwaitingAlternativeSolutionTroopsDepositedLocally>(Handle_AwaitingAlternativeSolutionTroopsDepositedLocally);
        messageBroker.Subscribe<RequestAwaitingAlternativeSolutionTroopsDeposit>(Handle_RequestAwaitingAlternativeSolutionTroopsDeposit);
        messageBroker.Subscribe<NetworkAwaitingAlternativeSolutionTroopsDepositRejected>(Handle_NetworkAwaitingAlternativeSolutionTroopsDepositRejected);
        messageBroker.Subscribe<NetworkAwaitingAlternativeSolutionTroopsDepositConfirmed>(Handle_NetworkAwaitingAlternativeSolutionTroopsDepositConfirmed);

        messageBroker.Subscribe<AwaitingAlternativeSolutionTroopsDrainedLocally>(Handle_AwaitingAlternativeSolutionTroopsDrainedLocally);
        messageBroker.Subscribe<RequestAwaitingAlternativeSolutionTroopsDrain>(Handle_RequestAwaitingAlternativeSolutionTroopsDrain);
        messageBroker.Subscribe<NetworkAwaitingAlternativeSolutionTroopsDrainConfirmed>(Handle_NetworkAwaitingAlternativeSolutionTroopsDrainConfirmed);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<AwaitingAlternativeSolutionTroopsDepositedLocally>(Handle_AwaitingAlternativeSolutionTroopsDepositedLocally);
        messageBroker.Unsubscribe<RequestAwaitingAlternativeSolutionTroopsDeposit>(Handle_RequestAwaitingAlternativeSolutionTroopsDeposit);
        messageBroker.Unsubscribe<NetworkAwaitingAlternativeSolutionTroopsDepositRejected>(Handle_NetworkAwaitingAlternativeSolutionTroopsDepositRejected);
        messageBroker.Unsubscribe<NetworkAwaitingAlternativeSolutionTroopsDepositConfirmed>(Handle_NetworkAwaitingAlternativeSolutionTroopsDepositConfirmed);

        messageBroker.Unsubscribe<AwaitingAlternativeSolutionTroopsDrainedLocally>(Handle_AwaitingAlternativeSolutionTroopsDrainedLocally);
        messageBroker.Unsubscribe<RequestAwaitingAlternativeSolutionTroopsDrain>(Handle_RequestAwaitingAlternativeSolutionTroopsDrain);
        messageBroker.Unsubscribe<NetworkAwaitingAlternativeSolutionTroopsDrainConfirmed>(Handle_NetworkAwaitingAlternativeSolutionTroopsDrainConfirmed);
    }

    private void Handle_AwaitingAlternativeSolutionTroopsDepositedLocally(MessagePayload<AwaitingAlternativeSolutionTroopsDepositedLocally> payload)
    {
        if (ModInformation.IsServer) return;
        if (!objectManager.TryGetIdWithLogging(payload.What.IssueOwner, out var ownerId)) return;

        var packed = troopRosterInterface.PackTroopRosterData(payload.What.Troops);
        network.SendAll(new RequestAwaitingAlternativeSolutionTroopsDeposit(ownerId, packed));
    }

    private void Handle_RequestAwaitingAlternativeSolutionTroopsDeposit(MessagePayload<RequestAwaitingAlternativeSolutionTroopsDeposit> payload)
    {
        if (ModInformation.IsClient) return;
        var requester = payload.Who as NetPeer;
        var request = payload.What;
        GameThread.RunSafe(() => ApplyDeposit(requester, request));
    }

    private void ApplyDeposit(NetPeer requester, RequestAwaitingAlternativeSolutionTroopsDeposit request)
    {
        if (requester == null || !playerManager.TryGetPlayer(requester, out var player))
        {
            Logger.Error("Rejecting {Message} from an unregistered/unknown requester", nameof(RequestAwaitingAlternativeSolutionTroopsDeposit));
            if (requester != null) network.Send(requester, new NetworkAwaitingAlternativeSolutionTroopsDepositRejected(request.OwnerId));
            return;
        }

        if (!objectManager.TryGetObjectWithLogging<Hero>(request.OwnerId, out var owner))
        {
            Logger.Error("Rejecting {Message} for an unknown owner {OwnerId}", nameof(RequestAwaitingAlternativeSolutionTroopsDeposit), request.OwnerId);
            SendCurrentPendingOrReject(requester, request.OwnerId, player.ControllerId);
            return;
        }

        if (!ownershipRegistry.TryGetOwnerControllerId(owner, out var recordedOwner) || recordedOwner != player.ControllerId)
        {
            Logger.Error("Rejecting {Message} from {Requester}, who is not the recorded owner of {Owner}",
                nameof(RequestAwaitingAlternativeSolutionTroopsDeposit), player.ControllerId, request.OwnerId);
            SendCurrentPendingOrReject(requester, request.OwnerId, player.ControllerId);
            return;
        }

        if (owner.Issue is not { IsSolvingWithAlternative: true } issue)
        {
            Logger.Error("Rejecting {Message} for {Owner}, whose issue is not solving with an alternative solution",
                nameof(RequestAwaitingAlternativeSolutionTroopsDeposit), request.OwnerId);
            SendCurrentPendingOrReject(requester, request.OwnerId, player.ControllerId);
            return;
        }

        if (!generationRegistry.TryGetGeneration(owner, out var currentGeneration))
        {
            Logger.Error("Rejecting {Message} for {Owner} - no tracked issue generation",
                nameof(RequestAwaitingAlternativeSolutionTroopsDeposit), request.OwnerId);
            SendCurrentPendingOrReject(requester, request.OwnerId, player.ControllerId);
            return;
        }

        if (depositedGenerationByOwnerId.TryGetValue(request.OwnerId, out var lastDepositedGeneration)
            && lastDepositedGeneration == currentGeneration)
        {
            SendCurrentPendingOrReject(requester, request.OwnerId, player.ControllerId);
            return;
        }

        var claimedRoster = UnpackToRoster(request.Troops);
        var validatedRoster = troopValidator.Validate(claimedRoster, issue.AlternativeSolutionSentTroops, preserveTroopXp: true);
        if (validatedRoster.Count == 0)
        {
            SendCurrentPendingOrReject(requester, request.OwnerId, player.ControllerId);
            return;
        }
        depositedGenerationByOwnerId[request.OwnerId] = currentGeneration;
        troopsRegistry.Deposit(player.ControllerId, validatedRoster);
        SendCurrentPendingOrReject(requester, request.OwnerId, player.ControllerId);
    }

    private void SendCurrentPendingOrReject(NetPeer requester, string ownerId, string controllerId)
    {
        if (!troopsRegistry.TryGet(controllerId, out var pending) ||
            !troopsRegistry.TryGetRevision(controllerId, out var revision))
        {
            network.Send(requester, new NetworkAwaitingAlternativeSolutionTroopsDepositRejected(ownerId));
            return;
        }
        var confirmedPacked = troopRosterInterface.PackTroopRosterData(pending);
        network.Send(requester, new NetworkAwaitingAlternativeSolutionTroopsDepositConfirmed(ownerId, confirmedPacked, revision));
    }

    private void Handle_NetworkAwaitingAlternativeSolutionTroopsDepositRejected(MessagePayload<NetworkAwaitingAlternativeSolutionTroopsDepositRejected> payload)
    {
        if (ModInformation.IsServer) return;
        var ownerId = payload.What.OwnerId;
        GameThread.RunSafe(() =>
        {
            if (!ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider)) return;
            var localControllerId = controllerIdProvider.ControllerId;
            if (string.IsNullOrEmpty(localControllerId)) return;
            Logger.Error("Server rejected {Message} for owner {OwnerId} - rolling back the local speculative deposit",
                nameof(RequestAwaitingAlternativeSolutionTroopsDeposit), ownerId);
            troopsRegistry.Clear(localControllerId);
        });
    }

    private void Handle_NetworkAwaitingAlternativeSolutionTroopsDepositConfirmed(MessagePayload<NetworkAwaitingAlternativeSolutionTroopsDepositConfirmed> payload)
    {
        if (ModInformation.IsServer) return;
        var confirmation = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider)) return;
            var localControllerId = controllerIdProvider.ControllerId;
            if (string.IsNullOrEmpty(localControllerId)) return;
            troopsRegistry.Restore(localControllerId, UnpackToRoster(confirmation.Troops), confirmation.Revision);
        });
    }

    private void Handle_AwaitingAlternativeSolutionTroopsDrainedLocally(MessagePayload<AwaitingAlternativeSolutionTroopsDrainedLocally> payload)
    {
        if (ModInformation.IsServer) return;
        if (!troopsRegistry.TryGetRevision(payload.What.OwnerControllerId, out var revision)) return;

        var packed = troopRosterInterface.PackTroopRosterData(payload.What.Troops);
        network.SendAll(new RequestAwaitingAlternativeSolutionTroopsDrain(packed, revision));
    }

    private void Handle_RequestAwaitingAlternativeSolutionTroopsDrain(MessagePayload<RequestAwaitingAlternativeSolutionTroopsDrain> payload)
    {
        if (ModInformation.IsClient) return;

        var requester = payload.Who as NetPeer;
        GameThread.RunSafe(() =>
        {
            if (requester == null || !playerManager.TryGetPlayer(requester, out var player))
            {
                Logger.Error("Rejecting {Message} from an unregistered/unknown requester", nameof(RequestAwaitingAlternativeSolutionTroopsDrain));
                return;
            }

            if (!troopsRegistry.TryGet(player.ControllerId, out var troops))
            {
                network.Send(requester, new NetworkAwaitingAlternativeSolutionTroopsDrainConfirmed(payload.What.Troops, payload.What.Revision));
                return;
            }
            if (!troopsRegistry.TryGetRevision(player.ControllerId, out var revision)) return;
            if (revision != payload.What.Revision)
            {
                var current = troopRosterInterface.PackTroopRosterData(troops);
                network.Send(requester, new NetworkAwaitingAlternativeSolutionTroopsDepositConfirmed(string.Empty, current, revision));
                return;
            }
            if (!RostersMatch(troops, UnpackToRoster(payload.What.Troops))) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero)) return;
            using (new MainHeroSubstitutionScope(hero, party))
            {
                if (!Campaign.Current.Models.IssueModel.CanTroopsReturnFromAlternativeSolution()) return;
                var confirmed = troopRosterInterface.PackTroopRosterData(troops);
                foreach (var element in troops.GetTroopRoster())
                {
                    var restoredByActivation = 0;
                    if (element.Character.IsHero && element.Character.HeroObject.HeroState != Hero.CharacterStates.Active)
                    {
                        var beforeActivation = party.MemberRoster.GetTroopCount(element.Character);
                        Logger.Warning("Issue3649ReturnActivation Hero={HeroId} AllowedThread={AllowedThread}",
                            element.Character.HeroObject.StringId, AllowedThread.IsThisThreadAllowed());
                        element.Character.HeroObject.ChangeState(Hero.CharacterStates.Active);
                        restoredByActivation = party.MemberRoster.GetTroopCount(element.Character) - beforeActivation;
                    }
                    party.MemberRoster.AddToCounts(element.Character, element.Number - restoredByActivation,
                        false, element.WoundedNumber, element.Xp, true);
                }
                troopsRegistry.Withdraw(player.ControllerId, troops);
                network.Send(requester, new NetworkAwaitingAlternativeSolutionTroopsDrainConfirmed(confirmed, revision));
            }
        });
    }

    private void Handle_NetworkAwaitingAlternativeSolutionTroopsDrainConfirmed(
        MessagePayload<NetworkAwaitingAlternativeSolutionTroopsDrainConfirmed> payload)
    {
        if (ModInformation.IsServer) return;
        var confirmation = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider)) return;
            if (!troopsRegistry.TryGetRevision(controllerIdProvider.ControllerId, out var revision) ||
                revision != confirmation.Revision) return;
            troopsRegistry.Withdraw(controllerIdProvider.ControllerId, UnpackToRoster(confirmation.Troops));
        });
    }

    private static bool RostersMatch(TroopRoster actual, TroopRoster requested)
    {
        if (actual.Count != requested.Count) return false;
        foreach (var element in actual.GetTroopRoster())
        {
            var index = requested.FindIndexOfTroop(element.Character);
            if (index < 0) return false;
            var other = requested.GetElementCopyAtIndex(index);
            if (element.Number != other.Number || element.WoundedNumber != other.WoundedNumber || element.Xp != other.Xp)
                return false;
        }
        return true;
    }

    private TroopRoster UnpackToRoster(TroopRosterData troops)
    {
        var roster = TroopRoster.CreateDummyTroopRoster();
        foreach (var element in troopRosterInterface.UnpackTroopRosterData(troops))
        {
            int index = roster.AddNewElement(element.Character, -1);
            roster.data[index] = element;
        }
        roster.UpdateVersion();
        roster.InitializeCachedData();

        return roster;
    }
}
