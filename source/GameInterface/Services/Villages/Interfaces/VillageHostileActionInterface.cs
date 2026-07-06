using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.MapEvents;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Villages.Data;
using GameInterface.Services.Villages.Messages;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Villages.Interfaces;

internal class VillageHostileActionInterface : IVillageHostileActionInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<VillageHostileActionInterface>();

    private const int ForceActionCooldownDays = 10;
    private static readonly TimeSpan MapEventStartApprovalTimeout = TimeSpan.FromSeconds(30);

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly ConcurrentDictionary<string, MapEventStartApproval> approvedMapEventStarts = new ConcurrentDictionary<string, MapEventStartApproval>();
    private readonly ConcurrentDictionary<string, bool> pendingHostileActionSettlements = new ConcurrentDictionary<string, bool>();
    private readonly ConcurrentDictionary<string, CampaignTime> forceActionCooldowns = new ConcurrentDictionary<string, CampaignTime>();
    private readonly ConditionalWeakTable<MapEvent, AppliedForceActionOutcomeState> appliedForceActionOutcomes = new ConditionalWeakTable<MapEvent, AppliedForceActionOutcomeState>();

    public VillageHostileActionInterface(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
    }

    public void RequestHostileAction(VillageHostileAction action)
    {
        var mobileParty = MobileParty.MainParty;
        var settlement = Settlement.CurrentSettlement;

        if (mobileParty == null || settlement == null)
        {
            Logger.Warning("Cannot request village hostile action without a current main party and settlement");
            return;
        }

        messageBroker.Publish(this, new VillageHostileActionAttempted(action, mobileParty, settlement));
    }

    public void BeginHostileActionPresentation(VillageHostileAction action)
    {
        var encounter = PlayerEncounter.Current;
        if (encounter == null)
        {
            Logger.Warning("Cannot begin village hostile action presentation without a current player encounter");
            return;
        }

        switch (action)
        {
            case VillageHostileAction.Raid:
                encounter.ForceRaid = true;
                break;
            case VillageHostileAction.ForceVolunteers:
                encounter.ForceVolunteers = true;
                break;
            case VillageHostileAction.ForceSupplies:
                encounter.ForceSupplies = true;
                break;
        }

        if (encounter._mapEvent == null)
        {
            var mapEvent = PlayerEncounter.StartBattle();
            if (mapEvent == null)
                Logger.Warning("Village hostile action presentation started without a map event");
        }

        GameMenu.SwitchToMenu("encounter");
    }

    public bool CanStartHostileAction(
        MobileParty mobileParty,
        Settlement settlement,
        VillageHostileAction action,
        out VillageHostileActionDeniedReason reason)
    {
        return CanStartHostileAction(mobileParty, settlement, action, allowPendingApproval: false, out reason);
    }

    private bool CanStartHostileAction(
        MobileParty mobileParty,
        Settlement settlement,
        VillageHostileAction action,
        bool allowPendingApproval,
        out VillageHostileActionDeniedReason reason)
    {
        reason = VillageHostileActionDeniedReason.Invalid;

        if (!IsKnownAction(action))
            return false;

        if (!TryValidateHostileActionRequester(mobileParty, out reason))
            return false;

        if (!TryValidateHostileActionSettlement(settlement, out reason))
            return false;

        if (!TryValidateHostileActionState(mobileParty, settlement, allowPendingApproval, out reason))
            return false;

        var village = settlement.Village;
        if (action == VillageHostileAction.ForceVolunteers && village.Hearth <= 0)
        {
            reason = VillageHostileActionDeniedReason.HearthTooLow;
            return false;
        }

        if (IsForceAction(action) && IsForceActionOnCooldown(settlement, out reason))
            return false;

        reason = VillageHostileActionDeniedReason.Invalid;
        return true;
    }

    private static bool TryValidateHostileActionRequester(MobileParty mobileParty, out VillageHostileActionDeniedReason reason)
    {
        reason = VillageHostileActionDeniedReason.Invalid;

        if (mobileParty?.Party == null)
        {
            reason = VillageHostileActionDeniedReason.InvalidRequester;
            return false;
        }

        if (!mobileParty.IsActive || mobileParty.Party.IsActive == false || mobileParty.LeaderHero?.IsPrisoner == true)
        {
            reason = VillageHostileActionDeniedReason.InvalidRequester;
            return false;
        }

        return true;
    }

    private static bool TryValidateHostileActionSettlement(Settlement settlement, out VillageHostileActionDeniedReason reason)
    {
        reason = VillageHostileActionDeniedReason.Invalid;

        if (settlement == null || settlement.IsVillage == false || settlement.Village == null || settlement.Party == null)
        {
            reason = VillageHostileActionDeniedReason.NonVillageSettlement;
            return false;
        }

        if (settlement.IsUnderSiege)
        {
            reason = VillageHostileActionDeniedReason.AlreadyInMapEvent;
            return false;
        }

        return true;
    }

    private bool TryValidateHostileActionState(
        MobileParty mobileParty,
        Settlement settlement,
        bool allowPendingApproval,
        out VillageHostileActionDeniedReason reason)
    {
        reason = VillageHostileActionDeniedReason.Invalid;

        if (mobileParty.MapEvent != null || settlement.Party.MapEvent != null)
        {
            reason = VillageHostileActionDeniedReason.AlreadyInMapEvent;
            return false;
        }

        if (mobileParty.MapFaction != null &&
            settlement.MapFaction != null &&
            mobileParty.MapFaction == settlement.MapFaction)
        {
            reason = VillageHostileActionDeniedReason.OwnFaction;
            return false;
        }

        if (settlement.Village.VillageState != Village.VillageStates.Normal)
        {
            reason = VillageHostileActionDeniedReason.InvalidVillageState;
            return false;
        }

        if (allowPendingApproval == false && HasPendingHostileActionApproval(settlement))
        {
            reason = VillageHostileActionDeniedReason.AlreadyInMapEvent;
            return false;
        }

        return true;
    }


    public void ApplyHostileAction(MobileParty mobileParty, Settlement settlement, VillageHostileAction action)
    {
        BeHostileAction.ApplyEncounterHostileAction(mobileParty.Party, settlement.Party);
    }

    public void ApplyForceActionOutcome(MapEvent mapEvent, VillageHostileAction action)
    {
        GameThread.RunSafe(
            () => ApplyForceActionOutcomeOnGameThread(mapEvent, action),
            blocking: true,
            context: nameof(ApplyForceActionOutcome));
    }

    private void ApplyForceActionOutcomeOnGameThread(MapEvent mapEvent, VillageHostileAction action)
    {
        if (mapEvent == null || !IsForceAction(action))
            return;

        if (!IsAttackerVictory(mapEvent))
            return;

        var attacker = mapEvent.AttackerSide?.LeaderParty?.MobileParty;
        var settlement = GetHostileActionSettlement(mapEvent);
        if (attacker == null || settlement?.Village == null)
            return;

        if (!TryMarkForceActionOutcomeApplied(mapEvent, action))
            return;

        switch (action)
        {
            case VillageHostileAction.ForceVolunteers:
                ApplyForceVolunteersOutcome(attacker, settlement);
                break;
            case VillageHostileAction.ForceSupplies:
                ApplyForceSuppliesOutcome(attacker, settlement, mapEvent);
                break;
        }
    }

    public void ApproveMapEventStart(PartyBase attacker, Settlement settlement, VillageHostileAction action)
    {
        if (!TryGetApprovalKey(attacker, settlement, action, out var key))
            return;

        if (!TryGetSettlementId(settlement, out var settlementId))
            return;

        approvedMapEventStarts[key] = new MapEventStartApproval(settlementId, DateTime.UtcNow + MapEventStartApprovalTimeout);
        pendingHostileActionSettlements[settlementId] = true;
    }

    public bool TryConsumeApprovedMapEventStart(
        PartyBase attacker,
        PartyBase defender,
        BattleCreationFlags flags,
        out VillageHostileActionDeniedReason reason)
    {
        reason = VillageHostileActionDeniedReason.Invalid;

        var hostileActionCount = GetHostileActionCount(flags, out var action);
        if (hostileActionCount == 0)
            return true;

        if (attacker?.MobileParty == null || defender?.Settlement == null)
        {
            reason = VillageHostileActionDeniedReason.Invalid;
            return false;
        }

        var settlement = defender.Settlement;
        if (hostileActionCount > 1)
        {
            CancelMapEventStartApprovals(attacker, settlement);
            reason = VillageHostileActionDeniedReason.Invalid;
            return false;
        }

        if (!CanStartHostileAction(attacker.MobileParty, settlement, action, allowPendingApproval: true, out reason))
        {
            CancelMapEventStartApprovals(attacker, settlement);
            return false;
        }

        if (!TryGetApprovalKey(attacker, settlement, action, out var key))
        {
            reason = VillageHostileActionDeniedReason.Invalid;
            return false;
        }

        if (approvedMapEventStarts.TryRemove(key, out var approval))
        {
            ClearPendingHostileActionApprovalIfNoApprovals(approval.SettlementId);
            if (approval.IsExpired)
            {
                reason = VillageHostileActionDeniedReason.NotApproved;
                return false;
            }

            return true;
        }

        PruneExpiredApprovals(settlement);
        reason = VillageHostileActionDeniedReason.NotApproved;
        return false;
    }

    public void CancelMapEventStartApprovals(PartyBase attacker)
    {
        if (attacker == null)
            return;

        if (!objectManager.TryGetId(attacker, out var attackerId))
            return;

        var keyPrefix = $"{attackerId}|";
        foreach (var pair in approvedMapEventStarts)
        {
            if (!pair.Key.StartsWith(keyPrefix, StringComparison.Ordinal))
                continue;

            if (approvedMapEventStarts.TryRemove(pair.Key, out var approval))
                ClearPendingHostileActionApprovalIfNoApprovals(approval.SettlementId);
        }
    }

    public bool TryGetForceActionCooldown(Settlement settlement, out CampaignTime cooldownUntil)
    {
        cooldownUntil = default;

        if (!TryGetSettlementId(settlement, out var settlementId))
            return false;

        if (!forceActionCooldowns.TryGetValue(settlementId, out cooldownUntil))
            return false;

        if (cooldownUntil.IsPast)
        {
            forceActionCooldowns.TryRemove(settlementId, out _);
            cooldownUntil = default;
            return false;
        }

        return true;
    }

    public VillageHostileActionCooldownData[] GetActiveCooldowns()
    {
        var cooldowns = new List<VillageHostileActionCooldownData>();
        foreach (var pair in forceActionCooldowns)
        {
            if (pair.Value.IsPast)
            {
                forceActionCooldowns.TryRemove(pair.Key, out _);
                continue;
            }

            cooldowns.Add(new VillageHostileActionCooldownData(pair.Key, pair.Value.NumTicks));
        }

        return cooldowns.ToArray();
    }

    public void ApplyCooldowns(VillageHostileActionCooldownData[] cooldowns)
    {
        var activeIds = new HashSet<string>();
        foreach (var cooldown in cooldowns ?? System.Array.Empty<VillageHostileActionCooldownData>())
        {
            if (string.IsNullOrWhiteSpace(cooldown.SettlementId))
                continue;

            var cooldownUntil = new CampaignTime(cooldown.CooldownUntilTicks);
            if (cooldownUntil.IsPast)
            {
                forceActionCooldowns.TryRemove(cooldown.SettlementId, out _);
                continue;
            }

            activeIds.Add(cooldown.SettlementId);
            forceActionCooldowns[cooldown.SettlementId] = cooldownUntil;
        }

        foreach (var settlementId in forceActionCooldowns.Keys)
        {
            if (!activeIds.Contains(settlementId))
                forceActionCooldowns.TryRemove(settlementId, out _);
        }
    }

    private void ApplyForceSuppliesOutcome(MobileParty mobileParty, Settlement settlement, MapEvent mapEvent)
    {
        var village = settlement.Village;
        var rewardUnits = MathF.Max((int)(village.Hearth * 0.15f), 20);
        var lootedItems = new ItemRoster();

        var productions = village.VillageType?.Productions;
        if ((productions == null || productions.Count == 0) && village.VillageType?._productions != null)
            productions = village.VillageType._productions;
        if (productions != null)
        {
            foreach (var production in productions)
            {
                var item = production.Item1;
                var count = (int)(production.Item2 / 60f * rewardUnits);
                if (item == null || count <= 0)
                    continue;

                var equipmentElement = new EquipmentElement(item);
                using (new AllowedThread())
                {
                    lootedItems.AddToCounts(equipmentElement, count);
                }

                using (AllowedThread.Suspend())
                {
                    mobileParty.Party.ItemRoster.AddToCounts(equipmentElement, count);
                }
            }
        }

        var leaderHero = mobileParty.LeaderHero;
        if (leaderHero != null)
        {
            var goldReward = rewardUnits * Campaign.Current.Models.RaidModel.GoldRewardForEachLostHearth;
            if (goldReward > 0)
            {
                using (AllowedThread.Suspend())
                {
                    GiveGoldAction.ApplyBetweenCharacters(null, leaderHero, goldReward, true);
                }
            }
        }

        SetForceActionCooldown(settlement);
        using (AllowedThread.Suspend())
        {
            settlement.SettlementHitPoints *= 0.2f;
        }
        SkillLevelingManager.OnForceSupplies(mobileParty, lootedItems, mapEvent.IsPlayerMapEvent == false);
    }

    private void ApplyForceVolunteersOutcome(MobileParty mobileParty, Settlement settlement)
    {
        var village = settlement.Village;
        var recruitCount = (int)MathF.Ceiling(village.Hearth / 30f);

        if (mobileParty.HasPerk(DefaultPerks.Roguery.InBestLight, false))
            recruitCount += settlement.Notables.Count;

        var troop = settlement.Culture?.BasicTroop;
        if (troop != null && recruitCount > 0)
        {
            using (AllowedThread.Suspend())
            {
                mobileParty.MemberRoster.AddToCounts(troop, recruitCount);
            }
        }

        SetForceActionCooldown(settlement);
        using (AllowedThread.Suspend())
        {
            settlement.SettlementHitPoints *= 0.2f;
            village.Hearth -= recruitCount / 2;
        }
        SkillLevelingManager.OnForceVolunteers(mobileParty, settlement.Party);
    }

    private static Settlement GetHostileActionSettlement(MapEvent mapEvent)
    {
        if (mapEvent.MapEventSettlement?.Village != null)
            return mapEvent.MapEventSettlement;

        if (mapEvent.DefenderSide?.LeaderParty?.Settlement?.Village != null)
            return mapEvent.DefenderSide.LeaderParty.Settlement;

        if (mapEvent.DefenderSide == null)
            return null;

        foreach (var mapEventParty in mapEvent.DefenderSide.Parties)
        {
            if (mapEventParty.Party?.Settlement?.Village != null)
                return mapEventParty.Party.Settlement;
        }

        return null;
    }

    private bool IsForceActionOnCooldown(Settlement settlement, out VillageHostileActionDeniedReason reason)
    {
        reason = VillageHostileActionDeniedReason.Invalid;

        if (!TryGetSettlementId(settlement, out _))
            return true;

        if (!TryGetForceActionCooldown(settlement, out _))
            return false;

        reason = VillageHostileActionDeniedReason.Cooldown;
        return true;
    }

    private void SetForceActionCooldown(Settlement settlement)
    {
        if (!TryGetSettlementId(settlement, out var settlementId))
            return;

        forceActionCooldowns[settlementId] = CampaignTime.DaysFromNow(ForceActionCooldownDays);
        messageBroker.Publish(this, new VillageHostileActionCooldownsChanged(GetActiveCooldowns()));
    }


    private sealed class MapEventStartApproval
    {
        public MapEventStartApproval(string settlementId, DateTime expiresAtUtc)
        {
            SettlementId = settlementId;
            ExpiresAtUtc = expiresAtUtc;
        }

        public string SettlementId { get; }
        public DateTime ExpiresAtUtc { get; }
        public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;
    }

    private bool TryGetSettlementId(Settlement settlement, out string settlementId)
    {
        settlementId = null;

        if (settlement == null)
            return false;

        return objectManager.TryGetId(settlement, out settlementId);
    }

    private bool HasPendingHostileActionApproval(Settlement settlement)
    {
        if (!TryGetSettlementId(settlement, out var settlementId))
            return false;

        PruneExpiredApprovals(settlementId);
        ClearPendingHostileActionApprovalIfNoApprovals(settlementId);
        return pendingHostileActionSettlements.ContainsKey(settlementId);
    }

    private void CancelMapEventStartApprovals(PartyBase attacker, Settlement settlement)
    {
        CancelMapEventStartApproval(attacker, settlement, VillageHostileAction.Raid);
        CancelMapEventStartApproval(attacker, settlement, VillageHostileAction.ForceVolunteers);
        CancelMapEventStartApproval(attacker, settlement, VillageHostileAction.ForceSupplies);

        if (TryGetSettlementId(settlement, out var settlementId))
            ClearPendingHostileActionApprovalIfNoApprovals(settlementId);
    }

    private void CancelMapEventStartApproval(PartyBase attacker, Settlement settlement, VillageHostileAction action)
    {
        if (!TryGetApprovalKey(attacker, settlement, action, out var key))
            return;

        if (approvedMapEventStarts.TryRemove(key, out var approval))
            ClearPendingHostileActionApprovalIfNoApprovals(approval.SettlementId);
    }

    private void PruneExpiredApprovals(Settlement settlement)
    {
        if (!TryGetSettlementId(settlement, out var settlementId))
            return;

        PruneExpiredApprovals(settlementId);
    }

    private void PruneExpiredApprovals(string settlementId)
    {
        var removed = false;
        foreach (var pair in approvedMapEventStarts)
        {
            if (pair.Value.SettlementId != settlementId || pair.Value.IsExpired == false)
                continue;

            if (approvedMapEventStarts.TryRemove(pair.Key, out _))
                removed = true;
        }

        if (removed)
            ClearPendingHostileActionApprovalIfNoApprovals(settlementId);
    }

    private void ClearPendingHostileActionApprovalIfNoApprovals(string settlementId)
    {
        foreach (var pair in approvedMapEventStarts)
        {
            if (pair.Value.SettlementId == settlementId && pair.Value.IsExpired == false)
                return;
        }

        pendingHostileActionSettlements.TryRemove(settlementId, out _);
    }

    private bool TryMarkForceActionOutcomeApplied(MapEvent mapEvent, VillageHostileAction action)
    {
        var state = appliedForceActionOutcomes.GetValue(mapEvent, _ => new AppliedForceActionOutcomeState());
        switch (action)
        {
            case VillageHostileAction.ForceVolunteers:
                if (state.ForceVolunteersApplied)
                    return false;

                state.ForceVolunteersApplied = true;
                return true;
            case VillageHostileAction.ForceSupplies:
                if (state.ForceSuppliesApplied)
                    return false;

                state.ForceSuppliesApplied = true;
                return true;
            default:
                return false;
        }
    }

    private sealed class AppliedForceActionOutcomeState
    {
        public bool ForceVolunteersApplied;
        public bool ForceSuppliesApplied;
    }

    private static bool IsAttackerVictory(MapEvent mapEvent)
    {
        return mapEvent.WinningSide == BattleSideEnum.Attacker ||
               mapEvent.BattleState == BattleState.AttackerVictory;
    }

    private static bool IsKnownAction(VillageHostileAction action)
    {
        return action == VillageHostileAction.Raid ||
               action == VillageHostileAction.ForceVolunteers ||
               action == VillageHostileAction.ForceSupplies;
    }

    private static bool IsForceAction(VillageHostileAction action)
    {
        return action == VillageHostileAction.ForceVolunteers ||
               action == VillageHostileAction.ForceSupplies;
    }

    private static int GetHostileActionCount(BattleCreationFlags flags, out VillageHostileAction action)
    {
        action = VillageHostileAction.Raid;

        var count = 0;
        if (flags.ForceRaid)
        {
            action = VillageHostileAction.Raid;
            count++;
        }

        if (flags.ForceVolunteers)
        {
            action = VillageHostileAction.ForceVolunteers;
            count++;
        }

        if (flags.ForceSupplies)
        {
            action = VillageHostileAction.ForceSupplies;
            count++;
        }

        return count;
    }

    private bool TryGetApprovalKey(PartyBase attacker, Settlement settlement, VillageHostileAction action, out string key)
    {
        key = null;

        if (attacker == null || settlement == null)
            return false;

        if (!objectManager.TryGetId(attacker, out var attackerId))
            return false;

        if (!objectManager.TryGetId(settlement, out var settlementId))
            return false;

        key = $"{attackerId}|{settlementId}|{(int)action}";
        return true;
    }
}
