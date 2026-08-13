using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Party;
using GameInterface.Services.Players;
using GameInterface.Services.TroopRosters.Interfaces;
using LiteNetLib;
using Serilog;
using TaleWorlds.CampaignSystem;
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
    private readonly IAwaitingAlternativeSolutionTroopsRegistry troopsRegistry;
    private readonly IPrisonerSaleValidator troopValidator;

    public AwaitingAlternativeSolutionTroopsHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        ITroopRosterInterface troopRosterInterface,
        IPlayerManager playerManager,
        IIssueOwnershipRegistry ownershipRegistry,
        IAwaitingAlternativeSolutionTroopsRegistry troopsRegistry,
        IPrisonerSaleValidator troopValidator)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.troopRosterInterface = troopRosterInterface;
        this.playerManager = playerManager;
        this.ownershipRegistry = ownershipRegistry;
        this.troopsRegistry = troopsRegistry;
        this.troopValidator = troopValidator;

        messageBroker.Subscribe<AwaitingAlternativeSolutionTroopsDepositedLocally>(Handle_AwaitingAlternativeSolutionTroopsDepositedLocally);
        messageBroker.Subscribe<RequestAwaitingAlternativeSolutionTroopsDeposit>(Handle_RequestAwaitingAlternativeSolutionTroopsDeposit);

        messageBroker.Subscribe<AwaitingAlternativeSolutionTroopsDrainedLocally>(Handle_AwaitingAlternativeSolutionTroopsDrainedLocally);
        messageBroker.Subscribe<RequestAwaitingAlternativeSolutionTroopsDrain>(Handle_RequestAwaitingAlternativeSolutionTroopsDrain);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<AwaitingAlternativeSolutionTroopsDepositedLocally>(Handle_AwaitingAlternativeSolutionTroopsDepositedLocally);
        messageBroker.Unsubscribe<RequestAwaitingAlternativeSolutionTroopsDeposit>(Handle_RequestAwaitingAlternativeSolutionTroopsDeposit);

        messageBroker.Unsubscribe<AwaitingAlternativeSolutionTroopsDrainedLocally>(Handle_AwaitingAlternativeSolutionTroopsDrainedLocally);
        messageBroker.Unsubscribe<RequestAwaitingAlternativeSolutionTroopsDrain>(Handle_RequestAwaitingAlternativeSolutionTroopsDrain);
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

        if (payload.Who is not NetPeer requester || !playerManager.TryGetPlayer(requester, out var player))
        {
            Logger.Error("Rejecting {Message} from an unregistered/unknown requester", nameof(RequestAwaitingAlternativeSolutionTroopsDeposit));
            return;
        }

        if (!objectManager.TryGetObjectWithLogging<Hero>(payload.What.OwnerId, out var owner))
        {
            Logger.Error("Rejecting {Message} for an unknown owner {OwnerId}", nameof(RequestAwaitingAlternativeSolutionTroopsDeposit), payload.What.OwnerId);
            return;
        }

        if (!ownershipRegistry.TryGetOwnerControllerId(owner, out var recordedOwner) || recordedOwner != player.ControllerId)
        {
            Logger.Error("Rejecting {Message} from {Requester}, who is not the recorded owner of {Owner}",
                nameof(RequestAwaitingAlternativeSolutionTroopsDeposit), player.ControllerId, payload.What.OwnerId);
            return;
        }

        if (owner.Issue is not { IsSolvingWithAlternative: true } issue)
        {
            Logger.Error("Rejecting {Message} for {Owner}, whose issue is not solving with an alternative solution",
                nameof(RequestAwaitingAlternativeSolutionTroopsDeposit), payload.What.OwnerId);
            return;
        }

        var claimedRoster = TroopRoster.CreateDummyTroopRoster();
        foreach (var element in troopRosterInterface.UnpackTroopRosterData(payload.What.Troops))
        {
            claimedRoster.AddToCounts(element.Character, element.Number, false, element.WoundedNumber, element.Xp, false);
        }

        var validatedRoster = troopValidator.Validate(claimedRoster, issue.AlternativeSolutionSentTroops);
        troopsRegistry.Deposit(player.ControllerId, validatedRoster);
    }

    private void Handle_AwaitingAlternativeSolutionTroopsDrainedLocally(MessagePayload<AwaitingAlternativeSolutionTroopsDrainedLocally> payload)
    {
        if (ModInformation.IsServer) return;

        network.SendAll(new RequestAwaitingAlternativeSolutionTroopsDrain());
    }

    private void Handle_RequestAwaitingAlternativeSolutionTroopsDrain(MessagePayload<RequestAwaitingAlternativeSolutionTroopsDrain> payload)
    {
        if (ModInformation.IsClient) return;

        if (payload.Who is not NetPeer requester || !playerManager.TryGetPlayer(requester, out var player))
        {
            Logger.Error("Rejecting {Message} from an unregistered/unknown requester", nameof(RequestAwaitingAlternativeSolutionTroopsDrain));
            return;
        }

        troopsRegistry.Clear(player.ControllerId);
    }
}
