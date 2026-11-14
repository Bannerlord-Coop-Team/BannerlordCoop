using Common;
using Common.Logging;
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
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Party.Handlers;

internal class PartyScreenCommitHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyScreenCommitHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ITroopRosterInterface troopRosterInterface;
    private readonly IPlayerManager playerManager;
    private readonly IAlternativeSolutionPicker picker;
    private readonly IPartyScreenCommandProcessor commandProcessor;

    public PartyScreenCommitHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ITroopRosterInterface troopRosterInterface,
        IPlayerManager playerManager,
        IAlternativeSolutionPicker picker,
        IPartyScreenCommandProcessor commandProcessor)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.troopRosterInterface = troopRosterInterface;
        this.playerManager = playerManager;
        this.picker = picker;
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
        var attempt = obj.What;
        // A sale only edits clones of the party, and PrisonersSold already sends what was sold.
        if (attempt.DoneKind == PartyScreenDoneKind.SellPrisoners) return;

        if (!objectManager.TryGetIdWithLogging(attempt.MainHero, out var mainHeroId)) return;

        string leftPartyId = null;
        if (attempt.LeftOwnerParty != null && !objectManager.TryGetIdWithLogging(attempt.LeftOwnerParty, out leftPartyId)) return;

        var commands = new List<PartyScreenCommandData>();
        foreach (var command in attempt.Commands)
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
            leftMemberSeed = troopRosterInterface.PackTroopRosterData(attempt.InitialLeftMembers);
            if (attempt.Mode != PartyScreenHelper.PartyScreenMode.PrisonerManage)
                leftPrisonerSeed = troopRosterInterface.PackTroopRosterData(attempt.InitialLeftPrisoners);
        }

        picker.TryGetIssue(attempt.LeftMemberRoster, out var issueOwnerId, out var issueId);

        network.SendAll(new NetworkCommitPartyScreen(
            mainHeroId,
            attempt.Mode,
            attempt.DoneKind,
            commands.ToArray(),
            troopRosterInterface.PackTroopRosterOrderData(attempt.RightMembers),
            leftPartyId,
            leftMemberSeed,
            leftPrisonerSeed,
            attempt.IsDonating,
            attempt.DoNotApplyGoldTransactions,
            attempt.ForceTransferId,
            issueOwnerId,
            issueId));
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

            IReadOnlyList<string> rejections;
            try
            {
                // The party can be gone by now, for example after a capture that landed before this commit.
                if (mainHero.PartyBelongedTo == null)
                {
                    rejections = new[] { "you no longer have a party" };
                }
                else
                {
                    rejections = commandProcessor.Apply(mainHero, message, requester);
                    messageBroker.Publish(this, new ApplyTroopRosterOrder(mainHero.PartyBelongedTo.MemberRoster, message.RightMemberOrderData));
                }
            }
            catch (Exception e)
            {
                // Changes made before the failure stay, so the player is told rather than left with a silent Done.
                Logger.Error(e, "Party screen commit failed for {Hero}", message.MainHeroId);
                rejections = new[] { "something failed on the server, some of them may not have gone through" };
            }

            if (rejections.Count > 0 && requester != null)
            {
                network.Send(requester, new SendInformationMessage(
                    "Some party changes couldn't be applied: " + string.Join("; ", rejections)));
            }
        }, context: nameof(PartyScreenCommitHandler));
    }
}
