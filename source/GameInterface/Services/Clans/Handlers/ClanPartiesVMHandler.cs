using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Transactions;
using Helpers;
using LiteNetLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Clans.Handlers;

internal class ClanPartiesVMHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ClanPartiesVMHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;

    public ClanPartiesVMHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;

        messageBroker.Subscribe<NewClanPartyCreated>(Handle_NewClanPartyCreated);
        messageBroker.Subscribe<CreateNewClanParty>(Handle_CreateNewClanParty);
        messageBroker.Subscribe<ClanPartyLeaderChanged>(Handle_ClanPartyLeaderChanged);
        messageBroker.Subscribe<ChangeClanPartyLeader>(Handle_ChangeClanPartyLeader);
        messageBroker.Subscribe<ClanPartyDisbanded>(Handle_ClanPartyDisbanded);
        messageBroker.Subscribe<DisbandClanParty>(Handle_DisbandClanParty);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NewClanPartyCreated>(Handle_NewClanPartyCreated);
        messageBroker.Unsubscribe<CreateNewClanParty>(Handle_CreateNewClanParty);
        messageBroker.Unsubscribe<ClanPartyLeaderChanged>(Handle_ClanPartyLeaderChanged);
        messageBroker.Unsubscribe<ChangeClanPartyLeader>(Handle_ChangeClanPartyLeader);
        messageBroker.Unsubscribe<ClanPartyDisbanded>(Handle_ClanPartyDisbanded);
        messageBroker.Unsubscribe<DisbandClanParty>(Handle_DisbandClanParty);
    }

    private void Handle_NewClanPartyCreated(MessagePayload<NewClanPartyCreated> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.NewLeader, out var newLeaderId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.TargetClan, out var targetClanId)) return;

        network.SendAll(new CreateNewClanParty(mainHeroId, newLeaderId, targetClanId, obj.What.PartyGoldLowerThreshold));
    }

    private void Handle_CreateNewClanParty(MessagePayload<CreateNewClanParty> obj)
    {
        NetPeer peer = obj.Who as NetPeer;
        GameThread.RunSafe(() => ServerTransactionOutcome.Execute(
            peer, ServerTransactionOutcome.ClanParty, () =>
        {
            if (!TryResolveAuthenticatedPlayer(
                    peer,
                    obj.What.MainHeroId,
                    null,
                    out Hero mainHero,
                    out MobileParty sourceParty,
                    out string reason) ||
                !objectManager.TryGetObjectWithLogging(
                    obj.What.NewLeaderId, out Hero newLeader) ||
                !objectManager.TryGetObjectWithLogging(
                    obj.What.TargetClanId, out Clan targetClan) ||
                targetClan != mainHero.Clan ||
                newLeader == mainHero ||
                newLeader.Clan != targetClan ||
                newLeader.PartyBelongedTo != null ||
                newLeader.PartyBelongedToAsPrisoner != null ||
                newLeader.IsChild ||
                !newLeader.CanLeadParty() ||
                !newLeader.CanBeGovernorOrHavePartyRole() ||
                newLeader.GovernorOf != null ||
                newLeader.HeroState != Hero.CharacterStates.Active ||
                sourceParty.MapEvent != null ||
                sourceParty.IsCurrentlyAtSea ||
                sourceParty.IsInRaftState ||
                targetClan.WarPartyComponents.Count >= targetClan.WarPartyLimit)
            {
                ServerTransactionOutcome.Reject(
                    peer, ServerTransactionOutcome.ClanParty,
                    reason ?? "The selected clan-party leader is no longer available.");
                return;
            }

            int mainGoldBefore = mainHero.Gold;
            int leaderGoldBefore = newLeader.Gold;
            int threshold = Campaign.Current.Models.ClanFinanceModel
                .PartyGoldLowerThreshold;
            int requiredGold = Math.Max(0, threshold - newLeader.Gold);
            if (mainHero.Gold < requiredGold)
            {
                ServerTransactionOutcome.Reject(
                    peer, ServerTransactionOutcome.ClanParty,
                    "You no longer have enough denars to create that party.");
                return;
            }

            MobileParty mobileParty = null;
            try
            {
                mobileParty = MobilePartyHelper.CreateNewClanMobileParty(
                    newLeader, targetClan);
                if (mobileParty == null)
                    throw new InvalidOperationException(
                        "The clan party was not created.");

                if (requiredGold > 0)
                    GiveGoldAction.ApplyBetweenCharacters(
                        mainHero, newLeader, requiredGold, false);
                mobileParty.SetMoveModeHold();
            }
            catch (Exception exception)
            {
                Logger.Error(
                    exception,
                    "Rolling back failed clan-party creation for {LeaderId}",
                    obj.What.NewLeaderId);
                mainHero.Gold = mainGoldBefore;
                newLeader.Gold = leaderGoldBefore;
                if (mobileParty?.IsActive == true)
                    DestroyPartyAction.Apply(null, mobileParty);
                ServerTransactionOutcome.Reject(
                    peer, ServerTransactionOutcome.ClanParty,
                    "The clan party could not be created safely.");
                return;
            }

            TryRefreshParties(peer);
            ServerTransactionOutcome.Accept(
                peer, ServerTransactionOutcome.ClanParty);
        }));
    }

    private void Handle_ClanPartyLeaderChanged(MessagePayload<ClanPartyLeaderChanged> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;

        string newLeaderId = null;
        if (obj.What.NewLeader != null && !objectManager.TryGetIdWithLogging(obj.What.NewLeader, out newLeaderId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.OldLeader, out var oldLeaderId)) return;

        string selectedPartyId = null;
        if (obj.What.SelectedParty != null && !objectManager.TryGetIdWithLogging(obj.What.SelectedParty, out selectedPartyId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.MainParty, out var mainPartyId)) return;

        network.SendAll(new ChangeClanPartyLeader(mainHeroId, newLeaderId, oldLeaderId, selectedPartyId, mainPartyId));
    }

    private void Handle_ChangeClanPartyLeader(MessagePayload<ChangeClanPartyLeader> obj)
    {
        NetPeer peer = obj.Who as NetPeer;
        GameThread.RunSafe(() => ServerTransactionOutcome.Execute(
            peer, ServerTransactionOutcome.ClanParty, () =>
        {
            if (!TryResolveAuthenticatedPlayer(
                    peer,
                    obj.What.MainHeroId,
                    obj.What.MainPartyId,
                    out Hero mainHero,
                    out MobileParty mainParty,
                    out string reason) ||
                !objectManager.TryGetObjectWithLogging(
                    obj.What.OldLeaderId, out Hero oldLeader) ||
                string.IsNullOrEmpty(obj.What.SelectedPartyId) ||
                !objectManager.TryGetObjectWithLogging(
                    obj.What.SelectedPartyId,
                    out MobileParty selectedParty) ||
                selectedParty == mainParty ||
                selectedParty?.IsActive != true ||
                selectedParty.ActualClan != mainHero.Clan ||
                selectedParty.LeaderHero != oldLeader ||
                selectedParty.MapEvent != null ||
                selectedParty.SiegeEvent != null ||
                selectedParty.Army != null ||
                selectedParty.IsCurrentlyAtSea ||
                selectedParty.IsInRaftState)
            {
                ServerTransactionOutcome.Reject(
                    peer, ServerTransactionOutcome.ClanParty,
                    reason ?? "The selected clan party no longer matches the server state.");
                return;
            }

            Hero newLeader = null;
            if (!string.IsNullOrEmpty(obj.What.NewLeaderId) &&
                (!objectManager.TryGetObjectWithLogging(
                    obj.What.NewLeaderId, out newLeader) ||
                 newLeader == mainHero ||
                 newLeader.Clan != mainHero.Clan ||
                 newLeader.PartyBelongedToAsPrisoner != null ||
                 newLeader.IsChild ||
                 !newLeader.CanLeadParty() ||
                 !newLeader.CanBeGovernorOrHavePartyRole() ||
                 newLeader.GovernorOf != null ||
                 newLeader.HeroState != Hero.CharacterStates.Active ||
                 newLeader.IsReleased ||
                 newLeader.IsFugitive ||
                 newLeader.IsTraveling ||
                 newLeader.Age < Campaign.Current.Models.AgeModel
                     .HeroComesOfAge ||
                 newLeader.CurrentSettlement?.IsUnderSiege == true ||
                 newLeader.CurrentSettlement?.IsUnderRaid == true ||
                 newLeader.PartyBelongedTo?.LeaderHero == newLeader ||
                 newLeader.PartyBelongedTo?.MapEvent != null ||
                 newLeader.PartyBelongedTo?.IsCurrentlyAtSea == true ||
                 newLeader.PartyBelongedTo?.IsInRaftState == true))
            {
                ServerTransactionOutcome.Reject(
                    peer, ServerTransactionOutcome.ClanParty,
                    "The new clan-party leader is no longer available.");
                return;
            }

            var isDisbanding = newLeader == null;
            int threshold = Campaign.Current.Models.ClanFinanceModel
                .PartyGoldLowerThreshold;
            int requiredGold = isDisbanding
                ? 0
                : Math.Max(0, threshold - newLeader.Gold);
            if (mainHero.Gold < requiredGold)
            {
                ServerTransactionOutcome.Reject(
                    peer, ServerTransactionOutcome.ClanParty,
                    "You no longer have enough denars to change that party leader.");
                return;
            }

            int mainGoldBefore = mainHero.Gold;
            int newLeaderGoldBefore = newLeader?.Gold ?? 0;
            MobileParty newLeaderSourceParty = newLeader?.PartyBelongedTo;
            var newLeaderSourceSettlement = newLeader?.CurrentSettlement;
            bool oldLeaderDetached = false;
            bool oldLeaderMoved = false;
            bool newLeaderMoved = false;
            try
            {
                if (isDisbanding)
                {
                    selectedParty.RemovePartyLeader();
                    oldLeaderDetached = true;
                    MakeHeroFugitiveAction.Apply(oldLeader, false);
                }
                else
                {
                    if (requiredGold > 0)
                        GiveGoldAction.ApplyBetweenCharacters(
                            mainHero, newLeader, requiredGold, false);
                    TeleportHeroAction.ApplyDelayedTeleportToParty(oldLeader, mainParty);
                    oldLeaderMoved = true;
                    TeleportHeroAction.ApplyDelayedTeleportToPartyAsPartyLeader(
                        newLeader, selectedParty);
                    newLeaderMoved = true;
                }
            }
            catch (Exception exception)
            {
                Logger.Error(
                    exception,
                    "Clan-party leader change failed for {PartyId}",
                    obj.What.SelectedPartyId);
                mainHero.Gold = mainGoldBefore;
                if (newLeader != null)
                    newLeader.Gold = newLeaderGoldBefore;
                try
                {
                    if (newLeaderMoved)
                    {
                        if (newLeaderSourceParty != null)
                            TeleportHeroAction.ApplyDelayedTeleportToParty(
                                newLeader, newLeaderSourceParty);
                        else if (newLeaderSourceSettlement != null)
                            TeleportHeroAction.ApplyDelayedTeleportToSettlement(
                                newLeader, newLeaderSourceSettlement);
                    }
                    if (oldLeaderMoved || oldLeaderDetached)
                    {
                        oldLeader.ChangeState(Hero.CharacterStates.Active);
                        TeleportHeroAction.ApplyDelayedTeleportToPartyAsPartyLeader(
                            oldLeader, selectedParty);
                    }
                }
                catch (Exception rollbackException)
                {
                    Logger.Error(
                        rollbackException,
                        "Clan-party leader rollback failed for {PartyId}",
                        obj.What.SelectedPartyId);
                }
                ServerTransactionOutcome.Reject(
                    peer, ServerTransactionOutcome.ClanParty,
                    "The clan-party leader could not be changed safely.");
                return;
            }

            ServerTransactionOutcome.Accept(
                peer, ServerTransactionOutcome.ClanParty);
        }));
    }

    private void Handle_ClanPartyDisbanded(MessagePayload<ClanPartyDisbanded> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.SelectedParty, out var selectedPartyId)) return;

        network.SendAll(new DisbandClanParty(selectedPartyId));
    }

    private void Handle_DisbandClanParty(MessagePayload<DisbandClanParty> obj)
    {
        NetPeer peer = obj.Who as NetPeer;
        GameThread.RunSafe(() => ServerTransactionOutcome.Execute(
            peer, ServerTransactionOutcome.ClanParty, () =>
        {
            if (!TryResolveAuthenticatedPlayer(
                    peer,
                    null,
                    null,
                    out Hero mainHero,
                    out MobileParty mainParty,
                    out string reason) ||
                !objectManager.TryGetObjectWithLogging(
                    obj.What.SelectedPartyId,
                    out MobileParty selectedParty) ||
                selectedParty == mainParty ||
                selectedParty?.IsActive != true ||
                selectedParty.ActualClan != mainHero.Clan ||
                selectedParty.IsMilitia ||
                selectedParty.IsGarrison ||
                selectedParty.IsDisbanding ||
                selectedParty.MapEvent != null ||
                selectedParty.SiegeEvent != null)
            {
                ServerTransactionOutcome.Reject(
                    peer, ServerTransactionOutcome.ClanParty,
                    reason ?? "The selected clan party is not yours to disband.");
                return;
            }

            try
            {
                DisbandPartyAction.StartDisband(selectedParty);
            }
            catch (Exception exception)
            {
                Logger.Error(
                    exception,
                    "Clan-party disband failed for {PartyId}",
                    obj.What.SelectedPartyId);
                ServerTransactionOutcome.Reject(
                    peer, ServerTransactionOutcome.ClanParty,
                    "The clan party could not be disbanded safely.");
                return;
            }

            TryRefreshParties(peer);
            ServerTransactionOutcome.Accept(
                peer, ServerTransactionOutcome.ClanParty);
        }));
    }

    private bool TryResolveAuthenticatedPlayer(
        NetPeer peer,
        string requestedHeroId,
        string requestedPartyId,
        out Hero mainHero,
        out MobileParty mainParty,
        out string reason)
    {
        mainHero = null;
        mainParty = null;
        reason = "The server could not authenticate this player.";
        if (!playerManager.TryGetPlayer(peer, out var player))
            return false;
        return ServerTransactionOutcome.TryResolvePlayer(
            peer,
            playerManager,
            objectManager,
            requestedHeroId ?? player.HeroId,
            requestedPartyId ?? player.MobilePartyId,
            out _,
            out mainHero,
            out mainParty,
            out reason);
    }

    private void TryRefreshParties(NetPeer peer)
    {
        try
        {
            network.Send(peer, new RefreshPartiesList());
        }
        catch (Exception exception)
        {
            Logger.Warning(
                exception,
                "Clan-party action committed, but the party list could not refresh");
        }
    }

}
