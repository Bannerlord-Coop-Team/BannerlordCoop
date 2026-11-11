using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using GameInterface.Services.CampaignService.Statistics.Interfaces;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Inventory.Interfaces;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.MapEvents.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Party.Data;
using GameInterface.Services.Party.Messages;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.TroopRosters.Data;
using GameInterface.Services.TroopRosters.Interfaces;
using GameInterface.Services.Villages.Interfaces;
using Helpers;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Party;

/// <summary>
/// Replays a player's party screen commands on the server, applying or rejecting each one on its own,
/// then runs the effects of the screen's vanilla done handler.
/// </summary>
internal interface IPartyScreenCommandProcessor
{
    /// <returns>A short description of every command that could not be applied in full.</returns>
    IReadOnlyList<string> Apply(Hero mainHero, NetworkCommitPartyScreen message, NetPeer requester);
}

internal class PartyScreenCommandProcessor : IPartyScreenCommandProcessor
{
    private const string NotInSettlement = "you are no longer in the settlement";
    private const string NotOnThisScreen = "not available on this screen";

    private static readonly ILogger Logger = LogManager.GetLogger<PartyScreenCommandProcessor>();

    private readonly IObjectManager objectManager;
    private readonly IMessageBroker messageBroker;
    private readonly ITroopRosterInterface troopRosterInterface;
    private readonly IRecruitmentSideEffects recruitmentSideEffects;
    private readonly IStatisticsCampaignBehaviorInterface statisticsInterface;
    private readonly IVillageHostileActionInterface villageHostileActionInterface;
    private readonly IAlternativeSolutionCommitments commitments;
    private readonly ISessionInventoryPlayerDataInterface sessionInventoryPlayerData;
    private readonly INetwork network;
    private readonly ISendCoalescer sendCoalescer;

    public PartyScreenCommandProcessor(
        IObjectManager objectManager,
        IMessageBroker messageBroker,
        ITroopRosterInterface troopRosterInterface,
        IRecruitmentSideEffects recruitmentSideEffects,
        IStatisticsCampaignBehaviorInterface statisticsInterface,
        IVillageHostileActionInterface villageHostileActionInterface,
        IAlternativeSolutionCommitments commitments,
        ISessionInventoryPlayerDataInterface sessionInventoryPlayerData,
        INetwork network,
        ISendCoalescer sendCoalescer = null)
    {
        this.network = network;
        this.sendCoalescer = sendCoalescer;
        this.objectManager = objectManager;
        this.messageBroker = messageBroker;
        this.troopRosterInterface = troopRosterInterface;
        this.recruitmentSideEffects = recruitmentSideEffects;
        this.statisticsInterface = statisticsInterface;
        this.villageHostileActionInterface = villageHostileActionInterface;
        this.commitments = commitments;
        this.sessionInventoryPlayerData = sessionInventoryPlayerData;
    }

    public IReadOnlyList<string> Apply(Hero mainHero, NetworkCommitPartyScreen message, NetPeer requester)
    {
        if (!TryCreateCommit(mainHero, message, requester, out var commit, out var failure))
        {
            Logger.Warning("Rejected party screen commit for {Hero}: {Reason}", mainHero.StringId, failure);
            return new[] { failure };
        }

        foreach (var command in message.Commands ?? Array.Empty<PartyScreenCommandData>())
        {
            if (!objectManager.TryGetObjectWithLogging<CharacterObject>(command.CharacterId, out var character)) continue;

            string rejection;
            try
            {
                rejection = command.Code switch
                {
                    PartyScreenLogic.PartyCommandCode.TransferTroop or
                    PartyScreenLogic.PartyCommandCode.TransferTroopToLeaderSlot => Transfer(commit, command, character),
                    PartyScreenLogic.PartyCommandCode.UpgradeTroop => commit.IsForceTransfer ? NotOnThisScreen : Upgrade(commit, command, character),
                    PartyScreenLogic.PartyCommandCode.RecruitTroop => commit.IsForceTransfer ? NotOnThisScreen : Recruit(commit, command, character),
                    _ => "not supported",
                };
            }
            catch (Exception e)
            {
                // One broken command shouldn't drop the rest of the screen, or leave the player without a reason.
                Logger.Error(e, "Party command {Code} {Character} failed for {Hero}", command.Code, character.StringId, mainHero.StringId);
                rejection = "failed on the server";
            }

            if (rejection == null) continue;

            Logger.Information(
                "Party command {Code} {Character} x{Number} for {Hero}: {Rejection}",
                command.Code, character.StringId, command.Number, mainHero.StringId, rejection);
            commit.Rejections.Add($"{character.Name} x{command.Number}: {rejection}");
        }

        Finish(commit, message, requester);
        return commit.Rejections;
    }

    private bool TryCreateCommit(
        Hero mainHero,
        NetworkCommitPartyScreen message,
        NetPeer requester,
        out Commit commit,
        out string failure)
    {
        commit = null;
        failure = null;
        var party = mainHero.PartyBelongedTo;
        var settlement = party.CurrentSettlement;

        // Leaving the settlement only costs the transfers into it, upgrades and recruits on the screen still apply.
        bool settlementGone = settlement?.Party == null && (
            message.IsDonating ||
            message.Mode == PartyScreenHelper.PartyScreenMode.PrisonerManage ||
            message.DoneKind == PartyScreenDoneKind.DonateGarrison);

        RosterSide leftMembers;
        RosterSide leftPrisoners;
        if (message.LeftPartyId != null)
        {
            if (!objectManager.TryGetObjectWithLogging<PartyBase>(message.LeftPartyId, out var leftParty))
            {
                failure = "the other party is gone";
                return false;
            }

            leftMembers = RosterSide.Real(leftParty.MemberRoster);
            leftPrisoners = RosterSide.Real(leftParty.PrisonRoster);
        }
        else
        {
            leftMembers = RosterSide.Virtual(troopRosterInterface.UnpackTroopRosterData(message.LeftMemberSeed));
            // The prisoner screens edit the settlement's own prison.
            leftPrisoners = message.Mode == PartyScreenHelper.PartyScreenMode.PrisonerManage && !settlementGone
                ? RosterSide.Real(settlement.Party.PrisonRoster)
                : RosterSide.Virtual(troopRosterInterface.UnpackTroopRosterData(message.LeftPrisonerSeed));
        }

        bool isForceTransfer = message.ForceTransferId != null;
        if (isForceTransfer)
        {
            // The volunteers the server set aside are the whole left side, whatever the client shows.
            if (!objectManager.TryGetId(party, out var partyId) ||
                !villageHostileActionInterface.TryConsumeForceTransfer(message.ForceTransferId, partyId, out var pool) ||
                !objectManager.TryGetObjectWithLogging<CharacterObject>(pool.TroopId, out var poolTroop))
            {
                failure = "The village has no recruits left to give.";
                return false;
            }

            leftMembers = RosterSide.Virtual(new[] { new TroopRosterElement(poolTroop) { _number = pool.TroopCount } });
        }
        else if (!string.IsNullOrEmpty(message.IssueId))
        {
            // Troops already picked for the quest are tracked here, companions come from the client's picker.
            var selected = commitments.GetSelection(requester, message.IssueOwnerId, message.IssueId);
            var heroes = troopRosterInterface.UnpackTroopRosterData(message.LeftMemberSeed).Where(element => element.Character.IsHero);
            leftMembers = RosterSide.Virtual(selected.Concat(heroes));
        }

        commit = new Commit(mainHero, message, settlement, leftMembers, leftPrisoners, isForceTransfer, settlementGone);
        return true;
    }

    private static string Transfer(Commit commit, PartyScreenCommandData command, CharacterObject character)
    {
        if (commit.SettlementGone) return NotInSettlement;
        bool isMember = command.Type == PartyScreenLogic.TroopType.Member;
        var hero = character.HeroObject;
        if (isMember && hero?.IsPlayerHero() == true) return "players can't be moved";
        if (!isMember && commit.IsForceTransfer) return NotOnThisScreen;

        var right = isMember ? commit.RightMembers : commit.RightPrisoners;
        var left = isMember ? commit.LeftMembers : commit.LeftPrisoners;
        bool fromRight = command.Side == PartyScreenLogic.PartyRosterSide.Right;
        // A prisoner hero leaving for or taken from a client-only left side changes hands in Finish,
        // through vanilla's release and capture, which move the hero themselves.
        bool deferred = !isMember && hero != null && left.IsVirtual &&
            (commit.DoneKind == PartyScreenDoneKind.Default || hero.IsPlayerHero());

        var available = fromRight ? ReadRight(commit, right, character) : left.Read(character);
        // A player is released through their captivity, which can still be running after a lost roster entry.
        if (deferred && fromRight && hero.IsPlayerHero() && hero.IsPrisoner &&
            (!commit.PendingRightPrisoners.TryGetValue(character, out var pendingRelease) || pendingRelease <= 0))
            available = (1, 0, 0);
        if (available.number <= 0) return fromRight ? "no longer in the party" : "no longer available";

        int number = Math.Min(command.Number, available.number);
        if (!isMember && fromRight && commit.IsDonating && !left.IsVirtual)
            number = Math.Min(number, commit.Settlement.Party.PrisonerSizeLimit - left.Roster.TotalManCount);
        if (number <= 0) return "no room left";

        int wounded = ClampWounded(command.WoundedNumber, number, available.number, available.wounded);
        int xpPerTroop = isMember
            ? GetMaxUpgradeXpCost(commit.Party, character)
            : Campaign.Current.Models.PrisonerRecruitmentCalculationModel.GetConformityNeededToRecruitPrisoner(character);
        int keptXp = (available.number - number) * xpPerTroop;
        int xp = fromRight
            ? (available.xp >= keptXp ? available.xp - keptXp : 0)
            : Math.Min(available.xp, number * xpPerTroop);

        int toLeft = fromRight ? number : -number;
        if (deferred)
        {
            left.Add(character, toLeft, fromRight ? wounded : -wounded, fromRight ? xp : -xp);
            AddCount(commit.PendingRightPrisoners, character, toLeft);
        }
        else
        {
            var source = fromRight ? right : left;
            var destination = fromRight ? left : right;
            source.Add(character, -number, -wounded, -xp);
            destination.Add(character, number, wounded, xp);
        }

        AddCount(isMember ? commit.NetMembersToLeft : commit.NetPrisonersToLeft, character, toLeft);
        return number < command.Number ? $"only {number} could be moved" : null;
    }

    // Deferred prisoner heroes are still in the right roster, but no longer available to move.
    private static (int number, int wounded, int xp) ReadRight(Commit commit, RosterSide right, CharacterObject character)
    {
        var state = right.Read(character);
        if (!ReferenceEquals(right, commit.RightPrisoners) ||
            !commit.PendingRightPrisoners.TryGetValue(character, out var pending))
            return state;

        int number = state.number - pending;
        return (number, Math.Min(state.wounded, Math.Max(number, 0)), state.xp);
    }

    private string Upgrade(Commit commit, PartyScreenCommandData command, CharacterObject character)
    {
        if (command.Side != PartyScreenLogic.PartyRosterSide.Right) return "only your own troops can be upgraded";
        if (command.UpgradeTarget < 0 || command.UpgradeTarget >= character.UpgradeTargets.Length) return "no upgrade target";

        var party = commit.Party;
        var roster = party.MemberRoster;
        int index = roster.FindIndexOfTroop(character);
        if (index < 0) return "no longer in the party";

        var element = roster.GetElementCopyAtIndex(index);
        var target = character.UpgradeTargets[command.UpgradeTarget];
        if (!Campaign.Current.Models.PartyTroopUpgradeModel.DoesPartyHaveRequiredPerksForUpgrade(party.Party, character, target, out _))
            return "missing the perk this upgrade needs";
        int xpCost = character.GetUpgradeXpCost(party.Party, command.UpgradeTarget);
        int goldCost = character.GetUpgradeGoldCost(party.Party, command.UpgradeTarget);
        var itemCategory = target.UpgradeRequiresItemFromCategory;

        int number = Math.Min(command.Number, element.Number);
        string shortOf = number < command.Number ? "troops" : null;
        if (xpCost > 0 && element.Xp / xpCost < number)
        {
            number = element.Xp / xpCost;
            shortOf = "experience";
        }
        if (goldCost > 0 && Math.Max(0, commit.MainHero.Gold + commit.GoldChange) / goldCost < number)
        {
            number = Math.Max(0, commit.MainHero.Gold + commit.GoldChange) / goldCost;
            shortOf = "gold";
        }
        if (itemCategory != null && CountItems(party.ItemRoster, itemCategory) < number)
        {
            number = CountItems(party.ItemRoster, itemCategory);
            shortOf = itemCategory.GetName().ToString();
        }
        if (number <= 0) return $"not enough {shortOf}";

        if (itemCategory != null)
            RemoveItems(party.ItemRoster, itemCategory, number, sessionInventoryPlayerData.GetInventoryLocks(commit.MainHeroId));
        roster.SetElementXp(index, element.Xp - (xpCost * number));
        commit.GoldChange -= goldCost * number;

        int healthy = element.Number - element.WoundedNumber;
        int wounded = number > healthy ? number - healthy : 0;
        roster.AddToCounts(character, -number, woundedCount: -wounded);
        roster.AddToCounts(target, number, woundedCount: wounded);
        SkillLevelingManager.OnUpgradeTroops(party.Party, character, target, number);

        return number < command.Number ? $"only {number} could be upgraded, not enough {shortOf}" : null;
    }

    private string Recruit(Commit commit, PartyScreenCommandData command, CharacterObject character)
    {
        if (command.Side != PartyScreenLogic.PartyRosterSide.Right ||
            command.Type != PartyScreenLogic.TroopType.Prisoner ||
            character.IsHero)
            return "can't be recruited";

        var party = commit.Party;
        if (party.PrisonRoster.FindIndexOfTroop(character) < 0) return "no longer a prisoner";

        var model = Campaign.Current.Models.PrisonerRecruitmentCalculationModel;
        int number = Math.Min(command.Number, model.CalculateRecruitableNumber(party.Party, character));
        if (number <= 0) return "not ready to be recruited";

        party.PrisonRoster.AddXpToTroop(character, -model.GetConformityNeededToRecruitPrisoner(character) * number);
        party.PrisonRoster.AddToCounts(character, -number);
        party.MemberRoster.AddToCounts(character, number);
        recruitmentSideEffects.Apply(party, character, number);
        party.RecentEventsMorale += model.GetPrisonerRecruitmentMoraleEffect(party.Party, character, number);
        commit.RecruitedCount += number;

        return number < command.Number ? $"only {number} could be recruited" : null;
    }

    private void Finish(Commit commit, NetworkCommitPartyScreen message, NetPeer requester)
    {
        var party = commit.Party;
        var settlement = commit.Settlement;

        // Player prisoners go home through the coop captivity release, which also restores their party.
        foreach (var entry in commit.PendingRightPrisoners)
        {
            if (entry.Value <= 0 || !entry.Key.HeroObject.IsPlayerHero()) continue;
            messageBroker.Publish(this, new PlayerCaptivityEndedByServer(
                entry.Key.HeroObject, EndCaptivityDetail.ReleasedByChoice, null, GetReleasePosition(party)));
        }

        var movedLeft = new FlattenedTroopRoster(4);
        // Players are left out because their captivity ended above.
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

        switch (commit.DoneKind)
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
                DonateToGarrison(commit, settlement);
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
        if (commit.IsDonating)
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
            messageBroker.Publish(this, new PartyScreenCommitApplied(
                requester, message.MainHeroId, message.IssueOwnerId, message.IssueId, PackLeftMemberDelta(commit)));
        }
    }

    private static void DonateToGarrison(Commit commit, Settlement settlement)
    {
        foreach (var entry in commit.LeftMembers.Entries)
        {
            if (entry.Value.number <= 0) continue;
            if (settlement.Town.GarrisonParty == null) settlement.AddGarrisonParty();
            settlement.Town.GarrisonParty.AddElementToMemberRoster(entry.Key, entry.Value.number, false);
            if (entry.Key.IsHero) EnterSettlementAction.ApplyForCharacterOnly(entry.Key.HeroObject, settlement);
        }
    }

    // Vanilla builds the party in OpenScreenAsCreateClanPartyForHeroPartyScreenClosed, from what the screen moved left.
    private void CreateClanParty(Commit commit)
    {
        var leader = commit.LeftMembers.Entries.Select(entry => entry.Key).LastOrDefault(character => character.IsHero)?.HeroObject;
        // The companion can be captured or killed while the screen is open.
        if (leader == null || leader.IsPlayerHero() || leader.PartyBelongedTo != commit.Party)
        {
            Logger.Error("Blocked clan party creation for {Hero}, the leader is missing, a player or no longer in the party", commit.MainHeroId);
            return;
        }
        // Vanilla only takes the leader out of the main party in a settlement, which a player's party never is on the server.
        if (leader.CurrentSettlement != null) commit.Party.MemberRoster.RemoveTroop(leader.CharacterObject);

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

    private TroopRosterData PackLeftMemberDelta(Commit commit)
    {
        var delta = new List<TroopRosterElementData>();
        foreach (var character in commit.LeftMembers.Entries.Select(entry => entry.Key).Union(commit.LeftMemberSeed.Keys))
        {
            var final = commit.LeftMembers.Read(character);
            commit.LeftMemberSeed.TryGetValue(character, out var seed);
            if (final == seed || !objectManager.TryGetHandleWithLogging(character, out var handle)) continue;
            delta.Add(new TroopRosterElementData(handle, final.number - seed.number, final.wounded - seed.wounded, final.xp - seed.xp));
        }

        return new TroopRosterData(delta);
    }

    private static void AddCount(Dictionary<CharacterObject, int> counts, CharacterObject character, int count)
    {
        counts.TryGetValue(character, out var current);
        counts[character] = current + count;
    }

    private static int ClampWounded(int requested, int number, int available, int availableWounded)
    {
        int min = Math.Max(0, number - (available - availableWounded));
        int max = Math.Min(number, availableWounded);
        return Math.Min(Math.Max(requested, min), max);
    }

    private static int GetMaxUpgradeXpCost(MobileParty party, CharacterObject character)
        => character.UpgradeTargets.Length == 0
            ? 0
            : character.UpgradeTargets.Max(target =>
                Campaign.Current.Models.PartyTroopUpgradeModel.GetXpCostForUpgrade(party.Party, character, target));

    private static int CountItems(ItemRoster itemRoster, ItemCategory category)
        => itemRoster
            .Where(item => item.EquipmentElement.Item?.ItemCategory == category)
            .Sum(item => item.Amount);

    // Like vanilla's RemoveItemFromItemRoster: unlocked items before the player's inventory locks, cheapest first.
    private static void RemoveItems(ItemRoster itemRoster, ItemCategory category, int count, IReadOnlyCollection<string> lockedItemIds)
    {
        var items = itemRoster
            .Where(item => item.EquipmentElement.Item?.ItemCategory == category)
            .OrderBy(item => lockedItemIds.Contains(GetItemLockId(item.EquipmentElement)))
            .ThenBy(item => item.EquipmentElement.Item.Value)
            .ToList();
        foreach (var item in items)
        {
            if (count <= 0) return;
            int removed = Math.Min(count, item.Amount);
            itemRoster.AddToCounts(item.EquipmentElement, -removed);
            count -= removed;
        }
    }

    // Same id the inventory screen saves its locks under, see PartyScreenLogic.GetItemLockStringID.
    private static string GetItemLockId(EquipmentElement element)
        => element.Item.StringId + (element.ItemModifier?.StringId ?? string.Empty);

    private static CampaignVec2 GetReleasePosition(MobileParty party)
        => party.CurrentSettlement != null ? party.CurrentSettlement.GatePosition : party.Position;

    /// <summary>
    /// One side of the screen: a real roster the server edits, or a roster only the client has, kept as counts.
    /// </summary>
    private sealed class RosterSide
    {
        private readonly Dictionary<CharacterObject, (int number, int wounded, int xp)> pool;

        private RosterSide(TroopRoster roster, Dictionary<CharacterObject, (int number, int wounded, int xp)> pool)
        {
            Roster = roster;
            this.pool = pool;
        }

        public TroopRoster Roster { get; }
        public bool IsVirtual => Roster == null;
        public IEnumerable<KeyValuePair<CharacterObject, (int number, int wounded, int xp)>> Entries
            => pool ?? Enumerable.Empty<KeyValuePair<CharacterObject, (int number, int wounded, int xp)>>();

        public static RosterSide Real(TroopRoster roster) => new(roster, null);

        public static RosterSide Virtual(IEnumerable<TroopRosterElement> seed)
        {
            var side = new RosterSide(null, new Dictionary<CharacterObject, (int number, int wounded, int xp)>());
            foreach (var element in seed)
                side.Add(element.Character, element.Number, element.WoundedNumber, element.Xp);
            return side;
        }

        public (int number, int wounded, int xp) Read(CharacterObject character)
        {
            if (IsVirtual)
            {
                pool.TryGetValue(character, out var pooled);
                return pooled;
            }

            int index = Roster.FindIndexOfTroop(character);
            if (index < 0) return default;
            var element = Roster.GetElementCopyAtIndex(index);
            return (element.Number, element.WoundedNumber, element.Xp);
        }

        public void Add(CharacterObject character, int number, int wounded, int xp)
        {
            if (IsVirtual)
            {
                pool.TryGetValue(character, out var pooled);
                pool[character] = (pooled.number + number, pooled.wounded + wounded, pooled.xp + xp);
                return;
            }

            Roster.AddToCounts(character, number, woundedCount: wounded, xpChange: xp);
        }
    }

    private sealed class Commit
    {
        public readonly Hero MainHero;
        public readonly string MainHeroId;
        public readonly MobileParty Party;
        public readonly Settlement Settlement;
        public readonly PartyScreenDoneKind DoneKind;
        public readonly bool IsDonating;
        public readonly bool IsForceTransfer;
        public readonly bool SettlementGone;
        public readonly RosterSide RightMembers;
        public readonly RosterSide RightPrisoners;
        public readonly RosterSide LeftMembers;
        public readonly RosterSide LeftPrisoners;
        public readonly Dictionary<CharacterObject, (int number, int wounded, int xp)> LeftMemberSeed;
        // Prisoner heroes moved off the right side whose roster change waits for Finish, negative when taken.
        public readonly Dictionary<CharacterObject, int> PendingRightPrisoners = new();
        public readonly Dictionary<CharacterObject, int> NetMembersToLeft = new();
        public readonly Dictionary<CharacterObject, int> NetPrisonersToLeft = new();
        public readonly List<string> Rejections = new();
        public int GoldChange;
        public int RecruitedCount;

        public Commit(
            Hero mainHero,
            NetworkCommitPartyScreen message,
            Settlement settlement,
            RosterSide leftMembers,
            RosterSide leftPrisoners,
            bool isForceTransfer,
            bool settlementGone)
        {
            MainHero = mainHero;
            MainHeroId = message.MainHeroId;
            Party = mainHero.PartyBelongedTo;
            Settlement = settlement;
            DoneKind = message.DoneKind;
            IsDonating = message.IsDonating;
            IsForceTransfer = isForceTransfer;
            SettlementGone = settlementGone;
            RightMembers = RosterSide.Real(Party.MemberRoster);
            RightPrisoners = RosterSide.Real(Party.PrisonRoster);
            LeftMembers = leftMembers;
            LeftPrisoners = leftPrisoners;
            LeftMemberSeed = leftMembers.Entries.ToDictionary(entry => entry.Key, entry => entry.Value);
        }
    }
}
