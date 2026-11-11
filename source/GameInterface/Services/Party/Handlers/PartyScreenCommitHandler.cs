using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Party.Data;
using GameInterface.Services.Party.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.TroopRosters.Data;
using GameInterface.Services.TroopRosters.Interfaces;
using GameInterface.Services.TroopRosters.Messages;
using Helpers;
using LiteNetLib;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Party.Handlers;

internal class PartyScreenCommitHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ITroopRosterInterface troopRosterInterface;
    private readonly IPlayerManager playerManager;
    private readonly IAlternativeSolutionPicker picker;
    private readonly IPartyScreenCommandTracker commandTracker;
    private readonly IPartyScreenCommandProcessor commandProcessor;

    public PartyScreenCommitHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ITroopRosterInterface troopRosterInterface,
        IPlayerManager playerManager,
        IAlternativeSolutionPicker picker,
        IPartyScreenCommandTracker commandTracker,
        IPartyScreenCommandProcessor commandProcessor)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.troopRosterInterface = troopRosterInterface;
        this.playerManager = playerManager;
        this.picker = picker;
        this.commandTracker = commandTracker;
        this.commandProcessor = commandProcessor;

        messageBroker.Subscribe<PartyScreenCommitAttempted>(Handle_PartyScreenCommitAttempted);
        messageBroker.Subscribe<NetworkCommitPartyScreen>(Handle_NetworkCommitPartyScreen);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PartyScreenCommitAttempted>(Handle_PartyScreenCommitAttempted);
        messageBroker.Unsubscribe<NetworkCommitPartyScreen>(Handle_NetworkCommitPartyScreen);
    }

    // Client
    private void Handle_PartyScreenCommitAttempted(MessagePayload<PartyScreenCommitAttempted> obj)
    {
        var logic = obj.What.Logic;
        var doneKind = GetDoneKind(logic);
        // A sale only edits clones of the party, and PrisonersSold already sends what was sold.
        if (doneKind == PartyScreenDoneKind.SellPrisoners) return;

        if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;

        string leftPartyId = null;
        if (logic.LeftOwnerParty != null && !objectManager.TryGetIdWithLogging(logic.LeftOwnerParty, out leftPartyId)) return;

        var commands = new List<PartyScreenCommandData>();
        foreach (var command in commandTracker.GetCommands(logic))
        {
            if (!objectManager.TryGetHandleWithLogging(command.Character, out var characterId)) continue;

            commands.Add(new PartyScreenCommandData(
                command.Code,
                command.RosterSide,
                command.Type,
                characterId,
                command.TotalNumber,
                command.WoundedNumber,
                command.UpgradeTarget));
        }

        // A real left party and the prisoner screens' settlement prison are known to the server, anything else
        // is a roster that only exists on this client, so send what it started with.
        TroopRosterData leftMemberSeed = default;
        TroopRosterData leftPrisonerSeed = default;
        if (leftPartyId == null)
        {
            leftMemberSeed = troopRosterInterface.PackTroopRosterData(logic._initialData.LeftMemberRoster);
            if (obj.What.Mode != PartyScreenHelper.PartyScreenMode.PrisonerManage)
                leftPrisonerSeed = troopRosterInterface.PackTroopRosterData(logic._initialData.LeftPrisonerRoster);
        }

        picker.TryGetIssue(logic.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Left], out var issueOwnerId, out var issueId);

        network.SendAll(new NetworkCommitPartyScreen(
            mainHeroId,
            obj.What.Mode,
            doneKind,
            commands.ToArray(),
            troopRosterInterface.PackTroopRosterOrderData(logic.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Right]),
            leftPartyId,
            leftMemberSeed,
            leftPrisonerSeed,
            obj.What.IsDonating,
            logic.DoNotApplyGoldTransactions,
            obj.What.ForceTransferId,
            issueOwnerId,
            issueId));
    }

    private static PartyScreenDoneKind GetDoneKind(PartyScreenLogic logic)
    {
        // The closed delegate is an event, so the screen is told apart by its transferable delegate.
        var transferable = logic.IsTroopTransferableDelegate?.Method;
        if (transferable?.DeclaringType == typeof(PartyScreenHelper) &&
            transferable.Name == nameof(PartyScreenHelper.OpenScreenAsCreateClanPartyForHeroTroopTransferableDelegate))
            return PartyScreenDoneKind.CreateClanParty;

        var method = logic.PartyPresentationDoneButtonDelegate?.Method;
        if (method?.DeclaringType != typeof(PartyScreenHelper)) return PartyScreenDoneKind.None;

        return method.Name switch
        {
            nameof(PartyScreenHelper.DefaultDoneHandler) => PartyScreenDoneKind.Default,
            nameof(PartyScreenHelper.SellPrisonersDoneHandler) => PartyScreenDoneKind.SellPrisoners,
            nameof(PartyScreenHelper.DonateGarrisonDoneHandler) => PartyScreenDoneKind.DonateGarrison,
            nameof(PartyScreenHelper.DonatePrisonersDoneHandler) => PartyScreenDoneKind.DonatePrisoners,
            nameof(PartyScreenHelper.ManageGarrisonDoneHandler) => PartyScreenDoneKind.ManageGarrison,
            _ => PartyScreenDoneKind.None,
        };
    }

    // Server
    private void Handle_NetworkCommitPartyScreen(MessagePayload<NetworkCommitPartyScreen> obj)
    {
        var message = obj.What;
        var requester = obj.Who as NetPeer;

        GameThread.RunSafe(() =>
        {
            if (!playerManager.IsSenderControllingHero(obj.Who, message.MainHeroId)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(message.MainHeroId, out var mainHero)) return;
            // The party can be gone by now, for example after a capture that landed before this commit.
            if (mainHero.PartyBelongedTo == null) return;

            var rejections = commandProcessor.Apply(mainHero, message, requester);
            messageBroker.Publish(this, new ApplyTroopRosterOrder(mainHero.PartyBelongedTo.MemberRoster, message.RightMemberOrderData));

            if (rejections.Count > 0 && requester != null)
            {
                network.Send(requester, new SendInformationMessage(
                    "Some party changes couldn't be applied: " + string.Join("; ", rejections)));
            }
        });
    }
}
