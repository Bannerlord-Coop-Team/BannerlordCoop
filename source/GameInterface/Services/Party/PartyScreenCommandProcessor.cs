using Common.Logging;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Inventory.Interfaces;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Party.Data;
using GameInterface.Services.Party.Messages;
using GameInterface.Services.Party.Patches;
using GameInterface.Services.TroopRosters.Interfaces;
using GameInterface.Services.Villages.Interfaces;
using Helpers;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GameInterface.Services.Party;

/// <summary>
/// Replays a player's party screen commands on the server, applying or rejecting each one on its own,
/// and hands the result to <see cref="IPartyScreenDoneEffects"/>.
/// </summary>
internal interface IPartyScreenCommandProcessor : IGameAbstraction
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
    private readonly ITroopRosterInterface troopRosterInterface;
    private readonly IRecruitmentSideEffects recruitmentSideEffects;
    private readonly IVillageHostileActionInterface villageHostileActionInterface;
    private readonly IAlternativeSolutionCommitments commitments;
    private readonly ISessionInventoryPlayerDataInterface sessionInventoryPlayerData;
    private readonly IPartyScreenDoneEffects doneEffects;

    public PartyScreenCommandProcessor(
        IObjectManager objectManager,
        ITroopRosterInterface troopRosterInterface,
        IRecruitmentSideEffects recruitmentSideEffects,
        IVillageHostileActionInterface villageHostileActionInterface,
        IAlternativeSolutionCommitments commitments,
        ISessionInventoryPlayerDataInterface sessionInventoryPlayerData,
        IPartyScreenDoneEffects doneEffects)
    {
        this.objectManager = objectManager;
        this.troopRosterInterface = troopRosterInterface;
        this.recruitmentSideEffects = recruitmentSideEffects;
        this.villageHostileActionInterface = villageHostileActionInterface;
        this.commitments = commitments;
        this.sessionInventoryPlayerData = sessionInventoryPlayerData;
        this.doneEffects = doneEffects;
    }

    public IReadOnlyList<string> Apply(Hero mainHero, NetworkCommitPartyScreen message, NetPeer requester)
    {
        if (!TryCreateCommit(mainHero, message, requester, out var commit, out var failure))
        {
            Logger.Warning("Rejected party screen commit for {Hero}: {Reason}", message.MainHeroId, failure);
            return new[] { failure };
        }

        foreach (var command in message.Commands ?? Array.Empty<PartyScreenCommandData>())
        {
            if (!objectManager.TryGetObjectWithLogging<CharacterObject>(command.CharacterId, out var character)) continue;

            string rejection = command.Code switch
            {
                _ when commit.IsForceTransfer &&
                    PartyScreenLogicPatches.IsBlockedOnForceTransferScreen(command.Code, command.Type) => NotOnThisScreen,
                PartyScreenLogic.PartyCommandCode.TransferTroop or
                PartyScreenLogic.PartyCommandCode.TransferTroopToLeaderSlot => Transfer(commit, command, character),
                PartyScreenLogic.PartyCommandCode.UpgradeTroop => Upgrade(commit, command, character),
                PartyScreenLogic.PartyCommandCode.RecruitTroop => Recruit(commit, command, character),
                _ => "not supported",
            };
            if (rejection == null) continue;

            Logger.Information(
                "Party command {Code} {Character} x{Number} for {Hero}: {Rejection}",
                command.Code, character.StringId, command.Number, message.MainHeroId, rejection);
            commit.Rejections.Add($"{character.Name} x{command.Number}: {rejection}");
        }

        doneEffects.Apply(commit);
        return commit.Rejections;
    }

    private bool TryCreateCommit(
        Hero mainHero,
        NetworkCommitPartyScreen message,
        NetPeer requester,
        out PartyScreenCommit commit,
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

        PartyBase leftParty = null;
        if (message.LeftPartyId != null && !objectManager.TryGetObjectWithLogging(message.LeftPartyId, out leftParty))
        {
            failure = "the other party is gone";
            return false;
        }

        // In order of precedence: the volunteers the server set aside are the whole left side, whatever the client
        // shows, then the troops already picked for a quest, then a real party, then what a client-only side held.
        PartyScreenRosterSide leftMembers;
        bool isForceTransfer = message.ForceTransferId != null;
        if (isForceTransfer)
        {
            if (!objectManager.TryGetId(party, out var partyId) ||
                !villageHostileActionInterface.TryConsumeForceTransfer(message.ForceTransferId, partyId, out var pool) ||
                !objectManager.TryGetObjectWithLogging<CharacterObject>(pool.TroopId, out var poolTroop))
            {
                failure = "the village has no recruits left to give";
                return false;
            }

            leftMembers = PartyScreenRosterSide.Virtual(new[] { new TroopRosterElement(poolTroop) { _number = pool.TroopCount } });
        }
        else if (!string.IsNullOrEmpty(message.IssueId))
        {
            // Companions sent on the quest come from the client's picker.
            var selected = commitments.GetSelection(requester, message.IssueOwnerId, message.IssueId);
            var heroes = troopRosterInterface.UnpackTroopRosterData(message.LeftMemberSeed).Where(element => element.Character.IsHero);
            leftMembers = PartyScreenRosterSide.Virtual(selected.Concat(heroes));
        }
        else if (leftParty != null)
        {
            leftMembers = PartyScreenRosterSide.Real(leftParty.MemberRoster);
        }
        else
        {
            leftMembers = PartyScreenRosterSide.Virtual(troopRosterInterface.UnpackTroopRosterData(message.LeftMemberSeed));
        }

        // The prisoner screens edit the settlement's own prison.
        PartyScreenRosterSide leftPrisoners;
        if (leftParty != null)
            leftPrisoners = PartyScreenRosterSide.Real(leftParty.PrisonRoster);
        else if (message.Mode == PartyScreenHelper.PartyScreenMode.PrisonerManage && !settlementGone)
            leftPrisoners = PartyScreenRosterSide.Real(settlement.Party.PrisonRoster);
        else
            leftPrisoners = PartyScreenRosterSide.Virtual(troopRosterInterface.UnpackTroopRosterData(message.LeftPrisonerSeed));

        commit = new PartyScreenCommit(message, requester, mainHero, settlement, settlementGone, isForceTransfer, leftMembers, leftPrisoners);

        // The companion can be captured or killed while the screen is open, so check before any troop moves.
        if (message.DoneKind == PartyScreenDoneKind.CreateClanParty)
        {
            var leader = commit.ClanPartyLeader;
            if (leader == null || leader.IsPlayerHero() || leader.PartyBelongedTo != party)
            {
                commit = null;
                failure = "the companion is no longer in your party";
                return false;
            }
        }

        return true;
    }

    private static string Transfer(PartyScreenCommit commit, PartyScreenCommandData command, CharacterObject character)
    {
        if (commit.SettlementGone) return NotInSettlement;
        bool isMember = command.Type == PartyScreenLogic.TroopType.Member;
        var hero = character.HeroObject;
        if (isMember && hero?.IsPlayerHero() == true) return "players can't be moved";

        var right = isMember ? commit.RightMembers : commit.RightPrisoners;
        var left = isMember ? commit.LeftMembers : commit.LeftPrisoners;
        bool fromRight = command.Side == PartyScreenLogic.PartyRosterSide.Right;
        // Positive moves troops to the left side.
        int sign = fromRight ? 1 : -1;
        bool deferred = !isMember && commit.IsDeferred(character);

        var available = fromRight ? ReadRight(commit, right, character) : left.Read(character);
        // A player prisoner is released through their captivity, which can still be running after their roster entry
        // was lost, so a release that isn't already pending counts them as there.
        bool releasesCaptivePlayer = deferred && fromRight && hero.IsPlayerHero() && hero.IsPrisoner &&
            GetCount(commit.NetPrisonersToLeft, character) <= 0;
        if (releasesCaptivePlayer) available = (1, 0, 0);
        if (available.number <= 0) return fromRight ? "no longer in the party" : "no longer available";

        int number = Math.Min(command.Number, available.number);
        if (!isMember && fromRight && commit.Message.IsDonating && !left.IsVirtual)
            number = Math.Min(number, commit.Settlement.Party.PrisonerSizeLimit - left.Roster.TotalManCount);
        if (number <= 0) return "no room left";

        int wounded = ClampWounded(command.WoundedNumber, number, available.number, available.wounded);
        int xpPerTroop = isMember
            ? GetMaxUpgradeXpCost(commit.Party, character)
            : Campaign.Current.Models.PrisonerRecruitmentCalculationModel.GetConformityNeededToRecruitPrisoner(character);
        int xp = TransferredXp(fromRight, number, available.number, available.xp, xpPerTroop);

        if (deferred)
        {
            // The hero's own roster entry moves in the done effects, so only the left side's count changes here.
            left.Add(character, sign * number, sign * wounded, sign * xp);
        }
        else
        {
            // The source goes first, because a hero leaving a party clears PartyBelongedTo whichever party it is.
            var source = fromRight ? right : left;
            var destination = fromRight ? left : right;
            source.Add(character, -number, -wounded, -xp);
            destination.Add(character, number, wounded, xp);
        }

        AddCount(isMember ? commit.NetMembersToLeft : commit.NetPrisonersToLeft, character, sign * number);
        return number < command.Number ? $"only {number} could be moved" : null;
    }

    // Deferred prisoner heroes are still in the right roster, but no longer available to move.
    private static (int number, int wounded, int xp) ReadRight(PartyScreenCommit commit, PartyScreenRosterSide right, CharacterObject character)
    {
        var state = right.Read(character);
        if (!ReferenceEquals(right, commit.RightPrisoners) || !commit.IsDeferred(character)) return state;

        int number = state.number - GetCount(commit.NetPrisonersToLeft, character);
        return (number, Math.Min(state.wounded, Math.Max(number, 0)), state.xp);
    }

    private string Upgrade(PartyScreenCommit commit, PartyScreenCommandData command, CharacterObject character)
    {
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

        var (number, shortOf) = LimitUpgrade(
            command.Number,
            element.Number,
            element.Xp,
            xpCost,
            commit.MainHero.Gold + commit.GoldChange,
            goldCost,
            itemCategory != null ? CountItems(party.ItemRoster, itemCategory) : null,
            itemCategory?.GetName().ToString());
        if (number <= 0) return $"not enough {shortOf}";

        if (itemCategory != null)
            RemoveItems(party.ItemRoster, itemCategory, number, sessionInventoryPlayerData.GetInventoryLocks(commit.Message.MainHeroId));
        roster.SetElementXp(index, element.Xp - (xpCost * number));
        commit.GoldChange -= goldCost * number;

        int healthy = element.Number - element.WoundedNumber;
        int wounded = number > healthy ? number - healthy : 0;
        roster.AddToCounts(character, -number, woundedCount: -wounded);
        roster.AddToCounts(target, number, woundedCount: wounded);
        SkillLevelingManager.OnUpgradeTroops(party.Party, character, target, number);

        return number < command.Number ? $"only {number} could be upgraded, not enough {shortOf}" : null;
    }

    private string Recruit(PartyScreenCommit commit, PartyScreenCommandData command, CharacterObject character)
    {
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

    /// <summary>
    /// How many of the requested upgrades the party can pay for, and what ran out first when that is fewer.
    /// <paramref name="items"/> is null when the upgrade needs no item.
    /// </summary>
    internal static (int number, string shortOf) LimitUpgrade(
        int requested, int troops, int xp, int xpCost, int gold, int goldCost, int? items, string itemName)
    {
        int number = Math.Min(requested, troops);
        string shortOf = number < requested ? "troops" : null;
        if (xpCost > 0 && xp / xpCost < number)
        {
            number = xp / xpCost;
            shortOf = "experience";
        }
        if (goldCost > 0 && Math.Max(0, gold) / goldCost < number)
        {
            number = Math.Max(0, gold) / goldCost;
            shortOf = "gold";
        }
        if (items.HasValue && items.Value < number)
        {
            number = items.Value;
            shortOf = itemName;
        }

        return (number, shortOf);
    }

    /// <summary>
    /// The xp that moves with <paramref name="number"/> troops. A right side stack keeps enough xp for the troops
    /// it still has, a left side stack gives up to what the moved troops can hold.
    /// </summary>
    internal static int TransferredXp(bool fromRight, int number, int available, int availableXp, int xpPerTroop)
    {
        if (fromRight)
        {
            int keptXp = (available - number) * xpPerTroop;
            return Math.Max(0, availableXp - keptXp);
        }

        return Math.Min(availableXp, number * xpPerTroop);
    }

    /// <summary>
    /// Fits a requested wounded count to a move of <paramref name="number"/> troops out of a stack, so the stack
    /// never gives more wounded than it has and keeps no more healthy troops than it had.
    /// </summary>
    internal static int ClampWounded(int requested, int number, int available, int availableWounded)
    {
        int min = Math.Max(0, number - (available - availableWounded));
        int max = Math.Min(number, availableWounded);
        return Math.Min(Math.Max(requested, min), max);
    }

    private static int GetCount(Dictionary<CharacterObject, int> counts, CharacterObject character)
        => counts.TryGetValue(character, out var count) ? count : 0;

    private static void AddCount(Dictionary<CharacterObject, int> counts, CharacterObject character, int count)
        => counts[character] = GetCount(counts, character) + count;

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
    internal static void RemoveItems(ItemRoster itemRoster, ItemCategory category, int count, IReadOnlyCollection<string> lockedItemIds)
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
    internal static string GetItemLockId(EquipmentElement element)
        => element.Item.StringId + (element.ItemModifier?.StringId ?? string.Empty);
}
