using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using GameInterface.Services.CampaignService.Statistics.Interfaces;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.MapEvents.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Party.Data;
using GameInterface.Services.PlayerCaptivityService.Messages;
using Helpers;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Party;

/// <summary>
/// Runs what the screen's vanilla done handler would, for a commit whose commands the server already replayed.
/// </summary>
internal interface IPartyScreenDoneEffects : IGameAbstraction
{
    void Apply(PartyScreenCommit commit);
}

internal class PartyScreenDoneEffects : IPartyScreenDoneEffects
{
    private readonly IObjectManager objectManager;
    private readonly IMessageBroker messageBroker;
    private readonly IStatisticsCampaignBehaviorInterface statisticsInterface;
    private readonly IAlternativeSolutionCommitments commitments;
    private readonly INetwork network;
    private readonly ISendCoalescer sendCoalescer;

    public PartyScreenDoneEffects(
        IObjectManager objectManager,
        IMessageBroker messageBroker,
        IStatisticsCampaignBehaviorInterface statisticsInterface,
        IAlternativeSolutionCommitments commitments,
        INetwork network,
        ISendCoalescer sendCoalescer = null)
    {
        this.objectManager = objectManager;
        this.messageBroker = messageBroker;
        this.statisticsInterface = statisticsInterface;
        this.commitments = commitments;
        this.network = network;
        this.sendCoalescer = sendCoalescer;
    }

    public void Apply(PartyScreenCommit commit)
    {
        var message = commit.Message;
        var party = commit.Party;
        var settlement = commit.Settlement;

        // Player prisoners go home through the coop captivity release, which also restores their party.
        foreach (var entry in commit.NetPrisonersToLeft)
        {
            if (entry.Value <= 0 || !commit.IsDeferred(entry.Key) || !entry.Key.HeroObject.IsPlayerHero()) continue;
            var releasePosition = party.CurrentSettlement != null ? party.CurrentSettlement.GatePosition : party.Position;
            messageBroker.Publish(this, new PlayerCaptivityEndedByServer(
                entry.Key.HeroObject, EndCaptivityDetail.ReleasedByChoice, null, releasePosition));
        }

        var (movedLeft, released, taken) = SortPrisoners(commit);
        switch (message.DoneKind)
        {
            case PartyScreenDoneKind.Default when commit.LeftPrisoners.IsVirtual:
                if (!released.IsEmpty<FlattenedTroopRosterElement>())
                    EndCaptivityAction.ApplyByReleasedByChoice(released);
                if (!taken.IsEmpty<FlattenedTroopRosterElement>())
                {
                    foreach (var character in taken.Troops)
                    {
                        if (character.IsHero) TakePrisonerAction.Apply(party.Party, character.HeroObject);
                    }
                    CampaignEventDispatcher.Instance.OnPrisonerTaken(taken);
                    statisticsInterface.OnPrisonersTaken(commit.MainHero, taken.Count());
                }
                break;
            case PartyScreenDoneKind.DonateGarrison when !commit.SettlementGone:
                DonateToGarrison(commit);
                break;
            case PartyScreenDoneKind.DonatePrisoners:
                EnterSettlementAsPrisoners(movedLeft, settlement);
                if (!movedLeft.IsEmpty<FlattenedTroopRosterElement>())
                    CampaignEventDispatcher.Instance.OnPrisonerDonatedToSettlement(party, movedLeft, settlement);
                break;
            case PartyScreenDoneKind.ManageGarrison:
                EnterSettlementAsPrisoners(movedLeft, settlement);
                foreach (var entry in commit.NetMembersToLeft)
                {
                    if (entry.Value > 0 && entry.Key.IsHero)
                        EnterSettlementAction.ApplyForCharacterOnly(entry.Key.HeroObject, settlement);
                }
                break;
            case PartyScreenDoneKind.CreateClanParty:
                CreateClanParty(commit);
                break;
        }

        if (settlement != null && !taken.IsEmpty<FlattenedTroopRosterElement>())
            CampaignEventDispatcher.Instance.OnPrisonersChangeInSettlement(settlement, taken, null, true);

        if (!message.DoNotApplyGoldTransactions && commit.GoldChange != 0)
            GiveGoldAction.ApplyBetweenCharacters(null, commit.MainHero, commit.GoldChange, false);

        // Vanilla only pays the troop part of the donation influence, prisoners pay through their own event.
        if (message.IsDonating)
        {
            var model = Campaign.Current.Models.PrisonerDonationModel;
            float influence = commit.NetMembersToLeft
                .Where(entry => entry.Value > 0)
                .Sum(entry => entry.Value * model.CalculateInfluenceGainAfterTroopDonation(party.Party, entry.Key, settlement));
            if ((int)influence != 0)
                GainKingdomInfluenceAction.ApplyForLeavingTroopToGarrison(commit.MainHero, (int)influence);
        }

        if (commit.RecruitedCount > 0)
            statisticsInterface.OnPrisonersRecruited(commit.MainHero, commit.RecruitedCount);

        if (!string.IsNullOrEmpty(message.IssueId))
        {
            var troops = commit.LeftMembers.Entries.Select(entry => new TroopRosterElement(entry.Key)
            {
                Number = entry.Value.number,
                WoundedNumber = entry.Value.wounded,
                Xp = entry.Value.xp,
            });
            commitments.SetSelection(commit.Requester, message.IssueOwnerId, message.IssueId, troops);
        }
    }

    /// <summary>
    /// Splits the prisoners the commands moved into those left on the left side, the ones released (players
    /// are left out because their captivity ended separately) and the ones taken off the left side.
    /// </summary>
    internal static (FlattenedTroopRoster movedLeft, FlattenedTroopRoster released, FlattenedTroopRoster taken) SortPrisoners(PartyScreenCommit commit)
    {
        var movedLeft = new FlattenedTroopRoster(4);
        var released = new FlattenedTroopRoster(4);
        var taken = new FlattenedTroopRoster(4);
        foreach (var entry in commit.NetPrisonersToLeft)
        {
            var hero = entry.Key.HeroObject;
            if (entry.Value > 0)
            {
                movedLeft.Add(entry.Key, entry.Value, 0);
                if (hero?.IsPlayerHero() != true) released.Add(entry.Key, entry.Value, 0);
            }
            else if (entry.Value < 0 && (hero == null || TakePrisonerActionPatches.CanCaptureHero(hero)))
                taken.Add(entry.Key, -entry.Value, 0);
        }

        return (movedLeft, released, taken);
    }

    private static void DonateToGarrison(PartyScreenCommit commit)
    {
        var settlement = commit.Settlement;
        foreach (var entry in commit.LeftMembers.Entries)
        {
            if (entry.Value.number <= 0) continue;
            if (settlement.Town.GarrisonParty == null) settlement.AddGarrisonParty();
            settlement.Town.GarrisonParty.AddElementToMemberRoster(entry.Key, entry.Value.number, false);
            if (entry.Key.IsHero) EnterSettlementAction.ApplyForCharacterOnly(entry.Key.HeroObject, settlement);
        }
    }

    // Vanilla builds the party in OpenScreenAsCreateClanPartyForHeroPartyScreenClosed, from what the screen moved left.
    private void CreateClanParty(PartyScreenCommit commit)
    {
        // MobilePartyHelperPatches takes the leader out of the player's party, also inside a settlement.
        var leader = commit.ClanPartyLeader;
        int partyGoldLowerThreshold = Campaign.Current.Models.ClanFinanceModel.PartyGoldLowerThreshold;
        if (leader.Gold < partyGoldLowerThreshold)
            GiveGoldAction.ApplyBetweenCharacters(commit.MainHero, leader, partyGoldLowerThreshold - leader.Gold, false);

        var clanParty = MobilePartyHelper.CreateNewClanMobileParty(leader, leader.Clan);
        foreach (var entry in commit.LeftMembers.Entries)
        {
            if (entry.Key != leader.CharacterObject && entry.Value.number > 0)
                clanParty.MemberRoster.AddToCounts(entry.Key, entry.Value.number, woundedCount: entry.Value.wounded, xpChange: entry.Value.xp);
        }
        // A player prisoner moved here is released by their captivity end instead.
        foreach (var entry in commit.LeftPrisoners.Entries)
        {
            if (entry.Value.number > 0 && entry.Key.HeroObject?.IsPlayerHero() != true)
                clanParty.PrisonRoster.AddToCounts(entry.Key, entry.Value.number, woundedCount: entry.Value.wounded, xpChange: entry.Value.xp);
        }

        // Sends the new party's troops now, so the clan screen shows the real count when it refreshes.
        if (objectManager.TryGetHandle(clanParty.MemberRoster, out var rosterId))
            sendCoalescer?.FlushInstance(rosterId, network);
        messageBroker.Publish(this, new ClanManagementChanged(clanParty.ActualClan, ClanManagementRefresh.Parties | ClanManagementRefresh.Members));
    }

    private static void EnterSettlementAsPrisoners(FlattenedTroopRoster prisoners, Settlement settlement)
    {
        foreach (var character in prisoners.Troops)
        {
            if (character.IsHero) EnterSettlementAction.ApplyForPrisoner(character.HeroObject, settlement);
        }
    }
}
