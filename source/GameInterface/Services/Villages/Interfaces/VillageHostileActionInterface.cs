using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Inventory.Data;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.MapEvents;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.TroopRosters.Data;
using GameInterface.Services.Villages.Data;
using GameInterface.Services.Villages.Messages;
using Helpers;
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
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Villages.Interfaces;

internal class VillageHostileActionInterface : IVillageHostileActionInterface, IDisposable
{
    private static readonly ILogger Logger = LogManager.GetLogger<VillageHostileActionInterface>();

    private const int ForceActionCooldownDays = 10;
    private static readonly TimeSpan MapEventStartApprovalTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ForceTransferPoolTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DeferredDrainMargin = TimeSpan.FromSeconds(60);

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly ConcurrentDictionary<string, MapEventStartApproval> approvedMapEventStarts = new ConcurrentDictionary<string, MapEventStartApproval>();
    private readonly ConcurrentDictionary<string, bool> pendingHostileActionSettlements = new ConcurrentDictionary<string, bool>();
    private readonly ConcurrentDictionary<string, CampaignTime> forceActionCooldowns = new ConcurrentDictionary<string, CampaignTime>();
    private readonly ConcurrentDictionary<string, PendingForceTransfer> pendingForceTransfers = new ConcurrentDictionary<string, PendingForceTransfer>();
    private readonly ConditionalWeakTable<MapEvent, AppliedForceActionOutcomeState> appliedForceActionOutcomes = new ConditionalWeakTable<MapEvent, AppliedForceActionOutcomeState>();
    private readonly object deferredForceScreenGate = new object();
    private DeferredForceScreen deferredForceScreen;

    public VillageHostileActionInterface(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        if (messageBroker == null) throw new ArgumentNullException(nameof(messageBroker));
        if (objectManager == null) throw new ArgumentNullException(nameof(objectManager));
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;

        messageBroker.Subscribe<CampaignTick>(Handle_CampaignTick);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<CampaignTick>(Handle_CampaignTick);
        lock (deferredForceScreenGate)
        {
            deferredForceScreen = null;
        }
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

        if (!mobileParty.IsActive || !mobileParty.Party.IsActive || mobileParty.LeaderHero?.IsPrisoner == true)
        {
            reason = VillageHostileActionDeniedReason.InvalidRequester;
            return false;
        }

        return true;
    }

    private static bool TryValidateHostileActionSettlement(Settlement settlement, out VillageHostileActionDeniedReason reason)
    {
        reason = VillageHostileActionDeniedReason.Invalid;

        if (settlement == null || !settlement.IsVillage || settlement.Village == null || settlement.Party == null)
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

        if (!allowPendingApproval && HasPendingHostileActionApproval(settlement))
        {
            reason = VillageHostileActionDeniedReason.AlreadyInMapEvent;
            return false;
        }

        return true;
    }

    public void ApplyHostileAction(MobileParty mobileParty, Settlement settlement, VillageHostileAction action)
    {
        BeHostileAction.ApplyEncounterHostileAction(mobileParty.Party, settlement.Party);

        if (!FactionManager.IsAtWarAgainstFaction(mobileParty.MapFaction, settlement.MapFaction))
            DeclareWarAction.ApplyByPlayerHostility(mobileParty.MapFaction, settlement.MapFaction);
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
        var rewardUnits = ComputeSuppliesRewardUnits(village.Hearth);
        var lootedItems = new ItemRoster();
        var poolItems = new List<ItemRosterElementData>();

        var productions = village.VillageType?.Productions;
        if ((productions == null || productions.Count == 0) && village.VillageType?._productions != null)
            productions = village.VillageType._productions;
        if (productions != null)
        {
            foreach (var production in productions)
            {
                var item = production.Item1;
                var count = ComputeSuppliesItemCount(production.Item2, rewardUnits);
                if (item == null || count <= 0)
                    continue;

                var equipmentElement = new EquipmentElement(item);
                using (new AllowedThread())
                {
                    lootedItems.AddToCounts(equipmentElement, count);
                }

                // The grant itself happens through the loot screen; only the pool snapshot is kept.
                if (!objectManager.TryGetId(item, out var itemObjectId))
                {
                    Logger.Warning("Skipping force supplies pool item without registry id (Settlement={SettlementId})", settlement.StringId);
                    continue;
                }

                poolItems.Add(new ItemRosterElementData(new ItemObjectData(itemObjectId, null, true), count));
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
        SkillLevelingManager.OnForceSupplies(mobileParty, lootedItems, !mapEvent.IsPlayerMapEvent);

        AuthorizeAndAnnounceTransfer(
            VillageHostileAction.ForceSupplies,
            mobileParty,
            settlement,
            poolItems.ToArray(),
            null,
            0);
    }

    private void ApplyForceVolunteersOutcome(MobileParty mobileParty, Settlement settlement)
    {
        var village = settlement.Village;
        var recruitCount = ComputeVolunteerCount(
            village.Hearth,
            mobileParty.HasPerk(DefaultPerks.Roguery.InBestLight, false),
            settlement.Notables.Count);

        var troop = settlement.Culture?.BasicTroop;
        string troopId = null;
        if (troop != null)
            objectManager.TryGetIdWithLogging(troop, out troopId);

        SetForceActionCooldown(settlement);
        using (AllowedThread.Suspend())
        {
            settlement.SettlementHitPoints *= 0.2f;
            village.Hearth -= recruitCount / 2;
        }
        SkillLevelingManager.OnForceVolunteers(mobileParty, settlement.Party);

        // The grant itself happens through the loot screen; only the pool snapshot is kept.
        AuthorizeAndAnnounceTransfer(
            VillageHostileAction.ForceVolunteers,
            mobileParty,
            settlement,
            Array.Empty<ItemRosterElementData>(),
            troopId,
            recruitCount);
    }

    private void AuthorizeAndAnnounceTransfer(
        VillageHostileAction action,
        MobileParty mobileParty,
        Settlement settlement,
        ItemRosterElementData[] suppliesItems,
        string troopId,
        int troopCount)
    {
        if (!objectManager.TryGetId(mobileParty, out var partyId) ||
            !objectManager.TryGetId(settlement, out var settlementId))
        {
            Logger.Warning("Skipping force transfer authorize without registry ids (Action={Action})", action);
            return;
        }

        var pool = AuthorizeForceTransfer(action, partyId, settlementId, suppliesItems, troopId, troopCount);
        messageBroker.Publish(this, new ForceTransferOutcomeReady(pool));
    }

    public ForceTransferPoolData AuthorizeForceTransfer(
        VillageHostileAction action,
        string partyId,
        string settlementId,
        ItemRosterElementData[] suppliesItems,
        string troopId,
        int troopCount,
        DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;
        PruneExpiredForceTransfers(now);

        var requestId = Guid.NewGuid().ToString("N");
        var pool = new ForceTransferPoolData(
            action,
            partyId,
            settlementId,
            requestId,
            suppliesItems ?? Array.Empty<ItemRosterElementData>(),
            troopId,
            troopCount);
        pendingForceTransfers[requestId] = new PendingForceTransfer(pool, now);
        return pool;
    }

    public bool TryConsumeForceTransfer(string requestId, string partyId, out ForceTransferPoolData pool)
    {
        pool = default;
        if (string.IsNullOrEmpty(requestId))
            return false;

        PruneExpiredForceTransfers(DateTime.UtcNow);
        if (!pendingForceTransfers.TryRemove(requestId, out var entry))
            return false;
        if (entry.Pool.PartyId != partyId)
        {
            // Wrong party: restore the entry so the rightful owner can still commit.
            pendingForceTransfers[requestId] = entry;
            return false;
        }

        pool = entry.Pool;
        Logger.Information(
            "ForceTransfer consumed (Request={RequestId}, Party={PartyId}, Pending={PendingCount})",
            requestId,
            partyId,
            pendingForceTransfers.Count);
        return true;
    }

    public bool TryPeekForceTransfer(string requestId, string partyId, out ForceTransferPoolData pool)
    {
        pool = default;
        if (string.IsNullOrEmpty(requestId))
            return false;

        PruneExpiredForceTransfers(DateTime.UtcNow);
        if (!pendingForceTransfers.TryGetValue(requestId, out var entry))
            return false;
        if (entry.Pool.PartyId != partyId)
            return false;

        pool = entry.Pool;
        return true;
    }

    public bool HasPendingForceTransferForParty(string partyId)
    {
        if (string.IsNullOrEmpty(partyId))
            return false;

        PruneExpiredForceTransfers(DateTime.UtcNow);
        foreach (var pair in pendingForceTransfers)
        {
            if (pair.Value.Pool.PartyId == partyId)
                return true;
        }

        return false;
    }

    public void GrantForceTransferPool(MobileParty attacker, ForceTransferPoolData pool)
    {
        if (!TryConsumeForceTransfer(pool.RequestId, pool.PartyId, out _))
        {
            Logger.Warning("Skipping force transfer fallback grant, pool already consumed (Request={RequestId})", pool.RequestId);
            return;
        }
        if (attacker == null)
            return;

        if (pool.Action == VillageHostileAction.ForceVolunteers &&
            !string.IsNullOrEmpty(pool.TroopId) &&
            pool.TroopCount > 0 &&
            objectManager.TryGetObjectWithLogging<CharacterObject>(pool.TroopId, out var troop))
        {
            using (AllowedThread.Suspend())
            {
                attacker.MemberRoster.AddToCounts(troop, pool.TroopCount);
            }
        }

        if (pool.Action == VillageHostileAction.ForceSupplies && pool.SuppliesItems != null)
        {
            foreach (var itemData in pool.SuppliesItems)
            {
                if (!objectManager.TryGetObjectWithLogging<ItemObject>(itemData.ItemObjectData.ItemObjectId, out var item))
                    continue;

                ItemModifier modifier = null;
                if (!itemData.ItemObjectData.ItemModifierNull &&
                    !objectManager.TryGetObjectWithLogging<ItemModifier>(itemData.ItemObjectData.ItemModifierId, out modifier))
                    continue;

                using (AllowedThread.Suspend())
                {
                    attacker.Party.ItemRoster.AddToCounts(new EquipmentElement(item, modifier), itemData.Amount);
                }
            }
        }

        Logger.Information(
            "ForceTransfer fallback auto-grant applied (Action={Action}, Party={PartyId}, Settlement={SettlementId})",
            pool.Action,
            pool.PartyId,
            pool.SettlementId);
    }

    internal void PruneExpiredForceTransfers(DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;
        foreach (var pair in pendingForceTransfers)
        {
            if (now - pair.Value.AuthorizedAtUtc <= ForceTransferPoolTimeout)
                continue;

            if (pendingForceTransfers.TryRemove(pair.Key, out _))
            {
                Logger.Information(
                    "ForceTransfer pending entry expired (Request={RequestId}, Party={PartyId}, Pending={PendingCount})",
                    pair.Key,
                    pair.Value.Pool.PartyId,
                    pendingForceTransfers.Count);
            }
        }
    }

    internal static int ComputeSuppliesRewardUnits(float hearth)
    {
        return MathF.Max((int)(hearth * 0.15f), 20);
    }

    internal static int ComputeSuppliesItemCount(float production, int rewardUnits)
    {
        return (int)(production / 60f * rewardUnits);
    }

    internal static int ComputeVolunteerCount(float hearth, bool hasInBestLight, int notables)
    {
        return (int)MathF.Ceiling(hearth / 30f) + (hasInBestLight ? notables : 0);
    }

    internal static bool IsForceTransferPoolValid(
        VillageHostileAction action,
        ItemRosterElementData[] suppliesItems,
        string troopId,
        int troopCount)
    {
        if (action == VillageHostileAction.ForceSupplies)
        {
            // An empty pool has nothing to show; deny so the client gets the
            // "nothing left" message instead of an empty loot screen.
            if (suppliesItems == null)
                return false;
            foreach (var item in suppliesItems)
            {
                if (item.Amount > 0)
                    return true;
            }

            return false;
        }

        return !string.IsNullOrEmpty(troopId) && troopCount > 0;
    }

    public bool IsForceTransferPoolValid(ForceTransferPoolData pool)
    {
        return IsForceTransferPoolValid(pool.Action, pool.SuppliesItems, pool.TroopId, pool.TroopCount);
    }

    public void OpenForceTransferLootScreen(ForceTransferPoolData pool)
    {
        // Opening a loot screen over a live battle mission yanks the game state out
        // from under the mission and breaks the normal battle-end flow, so park the
        // pool and open it from CampaignTick once the player has exited the mission.
        if (IsInMission())
        {
            ParkForceTransferScreen(pool);
            Logger.Information(
                "ForceTransfer loot screen deferred until mission end (Action={Action}, Settlement={SettlementId}, Request={RequestId})",
                pool.Action,
                pool.SettlementId,
                pool.RequestId);
            return;
        }

        OpenForceTransferLootScreenNow(pool);
    }

    private void Handle_CampaignTick(MessagePayload<CampaignTick> payload)
    {
        if (ModInformation.IsServer) return;
        // Keep the original parked time so a long mission cannot extend the entry past its expiry.
        if (IsInMission()) return;

        var now = DateTime.UtcNow;
        if (!TryTakeParkedForceTransferScreen(now, out var pool, out var parkedAt)) return;

        // The server pool expires on the server clock while parked here. Refuse to
        // open a screen whose remaining budget cannot cover a pick-and-Done round
        // trip; the player gets the deny message instead of a doomed screen.
        if (!IsParkedPoolFresh(parkedAt, now))
        {
            Logger.Information(
                "ForceTransfer deferred screen denied, pool budget exhausted (Request={RequestId}, Settlement={SettlementId})",
                pool.RequestId,
                pool.SettlementId);
            messageBroker.Publish(this, new SendInformationMessage("The village has nothing left to give."));
            return;
        }

        Logger.Information(
            "ForceTransfer deferred loot screen opened after mission (Action={Action}, Settlement={SettlementId}, Request={RequestId})",
            pool.Action,
            pool.SettlementId,
            pool.RequestId);
        OpenForceTransferLootScreenNow(pool);
    }

    internal static bool IsParkedPoolFresh(DateTime parkedAtUtc, DateTime utcNow)
    {
        return utcNow - parkedAtUtc <= ForceTransferPoolTimeout - DeferredDrainMargin;
    }

    internal void ParkForceTransferScreen(ForceTransferPoolData pool, DateTime? utcNow = null)
    {
        lock (deferredForceScreenGate)
        {
            if (deferredForceScreen != null)
            {
                Logger.Warning(
                    "ForceTransfer deferred screen replaced (OldRequest={OldRequestId}, NewRequest={NewRequestId})",
                    deferredForceScreen.Pool.RequestId,
                    pool.RequestId);
            }

            deferredForceScreen = new DeferredForceScreen(pool, utcNow ?? DateTime.UtcNow);
        }
    }

    internal bool TryTakeParkedForceTransferScreen(DateTime? utcNow, out ForceTransferPoolData pool, out DateTime parkedAtUtc)
    {
        lock (deferredForceScreenGate)
        {
            pool = default;
            parkedAtUtc = default;
            if (deferredForceScreen == null) return false;

            var now = utcNow ?? DateTime.UtcNow;
            if (now - deferredForceScreen.ParkedAtUtc > ForceTransferPoolTimeout)
            {
                Logger.Information(
                    "ForceTransfer deferred screen expired (Request={RequestId}, Settlement={SettlementId})",
                    deferredForceScreen.Pool.RequestId,
                    deferredForceScreen.Pool.SettlementId);
                deferredForceScreen = null;
                return false;
            }

            pool = deferredForceScreen.Pool;
            parkedAtUtc = deferredForceScreen.ParkedAtUtc;
            deferredForceScreen = null;
            return true;
        }
    }

    private static bool IsInMission()
    {
        return MissionState.Current != null || Mission.Current != null;
    }

    private sealed class DeferredForceScreen
    {
        public DeferredForceScreen(ForceTransferPoolData pool, DateTime parkedAtUtc)
        {
            Pool = pool;
            ParkedAtUtc = parkedAtUtc;
        }

        public ForceTransferPoolData Pool { get; }
        public DateTime ParkedAtUtc { get; }
    }

    private void OpenForceTransferLootScreenNow(ForceTransferPoolData pool)
    {
        if (pool.Action == VillageHostileAction.ForceSupplies)
        {
            var dummy = new ItemRoster();
            using (new AllowedThread())
            {
                foreach (var itemData in pool.SuppliesItems ?? Array.Empty<ItemRosterElementData>())
                {
                    if (itemData.Amount <= 0 ||
                        !objectManager.TryGetObject<ItemObject>(itemData.ItemObjectData.ItemObjectId, out var item))
                    {
                        Logger.Warning("Skipping force supplies screen item (Item={ItemId})", itemData.ItemObjectData.ItemObjectId);
                        continue;
                    }

                    ItemModifier modifier = null;
                    if (!itemData.ItemObjectData.ItemModifierNull &&
                        !objectManager.TryGetObject<ItemModifier>(itemData.ItemObjectData.ItemModifierId, out modifier))
                    {
                        Logger.Warning("Skipping force supplies screen item without modifier (Item={ItemId})", itemData.ItemObjectData.ItemObjectId);
                        continue;
                    }

                    dummy.AddToCounts(new EquipmentElement(item, modifier), itemData.Amount);
                }
            }

            if (dummy.Count == 0)
            {
                // The pool was non-empty at authorize time, but nothing resolved
                // locally. Open no screen; the deny message mirrors the server path.
                Logger.Warning(
                    "ForceTransfer supplies screen denied, dummy roster empty (Settlement={SettlementId}, Request={RequestId})",
                    pool.SettlementId,
                    pool.RequestId);
                messageBroker.Publish(this, new SendInformationMessage("The village has nothing left to give."));
                return;
            }

            ForceTransferScreenTracker.NoteLootScreenOpened(pool.RequestId, dummy);
            InventoryScreenHelper.OpenScreenAsLoot(new Dictionary<PartyBase, ItemRoster>
            {
                { PartyBase.MainParty, dummy }
            });
            Logger.Information(
                "ForceTransfer supplies screen opened (Settlement={SettlementId}, Request={RequestId})",
                pool.SettlementId,
                pool.RequestId);
            return;
        }

        if (pool.Action == VillageHostileAction.ForceVolunteers)
        {
            if (string.IsNullOrEmpty(pool.TroopId) ||
                pool.TroopCount <= 0 ||
                !objectManager.TryGetObjectWithLogging<CharacterObject>(pool.TroopId, out var troop) ||
                !objectManager.TryGetObject<Settlement>(pool.SettlementId, out var settlement))
            {
                messageBroker.Publish(this, new SendInformationMessage("The village has no recruits left to give."));
                return;
            }

            TroopRoster leftMember;
            using (new AllowedThread())
            {
                leftMember = TroopRoster.CreateDummyTroopRoster();
                leftMember.AddToCounts(troop, pool.TroopCount);
            }

            ForceTransferScreenTracker.NoteLootScreenOpened(pool.RequestId, leftMember);
            PartyScreenHelper.OpenScreenAsLoot(leftMember, TroopRoster.CreateDummyTroopRoster(), settlement.Name, pool.TroopCount, null);
            Logger.Information(
                "ForceTransfer volunteers screen opened (Settlement={SettlementId}, Request={RequestId})",
                pool.SettlementId,
                pool.RequestId);
        }
    }

    internal static bool TryValidateSuppliesTake(
        ItemRosterElementData[] poolItems,
        IEnumerable<(ItemRosterElementData item, int price)> boughtItems,
        out string error)
    {
        error = null;
        var pool = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var poolItem in poolItems ?? Array.Empty<ItemRosterElementData>())
        {
            var key = GetSuppliesPoolKey(poolItem);
            pool.TryGetValue(key, out var pooled);
            pool[key] = pooled + Math.Max(0, poolItem.Amount);
        }

        var taken = new Dictionary<string, int>(StringComparer.Ordinal);
        if (boughtItems != null)
        {
            foreach (var (item, _) in boughtItems)
            {
                var key = GetSuppliesPoolKey(item);
                taken.TryGetValue(key, out var count);
                taken[key] = count + Math.Max(0, item.Amount);
            }
        }

        foreach (var pair in taken)
        {
            if (pair.Value <= 0)
                continue;
            pool.TryGetValue(pair.Key, out var allowed);
            if (pair.Value > allowed)
            {
                error = $"take of {pair.Value} x {pair.Key} exceeds authorized pool of {allowed}";
                return false;
            }
        }

        return true;
    }

    internal static bool TryValidateVolunteersTake(
        string troopId,
        int troopCount,
        TroopRosterData rightMemberDelta,
        out string error,
        IEnumerable<(string fromId, string toId, int number)> upgradedTroops = null)
    {
        error = null;
        if (string.IsNullOrEmpty(troopId) || troopCount <= 0)
        {
            error = "no authorized recruit pool";
            return false;
        }

        var gained = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var element in rightMemberDelta.Data ?? Array.Empty<TroopRosterElementData>())
        {
            if (element.Number <= 0)
                continue;
            gained.TryGetValue(element.CharacterId, out var count);
            gained[element.CharacterId] = count + element.Number;
        }

        gained.TryGetValue(troopId, out var poolGained);
        if (poolGained > troopCount)
        {
            error = $"take of {poolGained} x {troopId} exceeds authorized pool of {troopCount}";
            return false;
        }

        // Gains of any other troop are only legitimate as recorded upgrades made
        // on the screen (e.g. upgrading a taken recruit); anything else is rejected.
        var upgraded = new Dictionary<string, int>(StringComparer.Ordinal);
        if (upgradedTroops != null)
        {
            foreach (var (fromId, toId, number) in upgradedTroops)
            {
                if (string.IsNullOrEmpty(toId) || number <= 0)
                    continue;
                upgraded.TryGetValue(toId, out var count);
                upgraded[toId] = count + number;
            }
        }

        foreach (var pair in gained)
        {
            if (pair.Key == troopId)
                continue;
            upgraded.TryGetValue(pair.Key, out var covered);
            if (pair.Value > covered)
            {
                error = $"gain of {pair.Value} x {pair.Key} is outside the authorized pool of {troopCount} x {troopId}";
                return false;
            }
        }

        return true;
    }

    // Bounds the total outflow from the dummy left roster (pool minus remainder),
    // covering takes that bypass BoughtItems (e.g. straight-to-equipment moves).
    // The server drops left-side deltas for unregistered dummies, so the remainder
    // itself grants nothing; this only proves nothing left the pool unaccounted for.
    internal static bool TryValidateSuppliesLeftRemainder(
        ItemRosterElementData[] poolItems,
        IEnumerable<(string itemId, string modifierId, int amount)> leftRemainder,
        out string error)
    {
        error = null;
        var pool = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var poolItem in poolItems ?? Array.Empty<ItemRosterElementData>())
        {
            var key = GetSuppliesPoolKey(poolItem);
            pool.TryGetValue(key, out var pooled);
            pool[key] = pooled + Math.Max(0, poolItem.Amount);
        }

        if (leftRemainder == null)
            return true;

        foreach (var (itemId, modifierId, amount) in leftRemainder)
        {
            if (amount < 0)
            {
                error = $"left remainder of {amount} x {itemId} is negative";
                return false;
            }
            if (amount == 0)
                continue;
            var key = GetSuppliesPoolKey(itemId, modifierId);
            pool.TryGetValue(key, out var allowed);
            if (amount > allowed)
            {
                error = $"left remainder of {amount} x {key} exceeds authorized pool of {allowed}";
                return false;
            }
        }

        return true;
    }

    // Joint bound: take (BoughtItems inflow) plus leftover (left remainder) must
    // fit inside the pool per (item, modifier) key, so a duplicated take cannot
    // pass the two independent checks. Sold items (player gifts into the dummy)
    // are not credited: they inflate the remainder and can only make this
    // stricter, never looser. This stays honest-client/resend protection, not
    // anti-cheat: the applied To-roster/equipment snapshots have no server-side
    // baseline to diff against, so fabricated snapshots beyond the histories
    // rely on the trusted-client model.
    internal static bool TryValidateSuppliesTakeAndRemainder(
        ItemRosterElementData[] poolItems,
        IEnumerable<(ItemRosterElementData item, int price)> boughtItems,
        IEnumerable<(string itemId, string modifierId, int amount)> leftRemainder,
        out string error)
    {
        error = null;
        var pool = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var poolItem in poolItems ?? Array.Empty<ItemRosterElementData>())
        {
            var key = GetSuppliesPoolKey(poolItem);
            pool.TryGetValue(key, out var pooled);
            pool[key] = pooled + Math.Max(0, poolItem.Amount);
        }

        var held = new Dictionary<string, int>(StringComparer.Ordinal);
        if (boughtItems != null)
        {
            foreach (var (item, _) in boughtItems)
            {
                if (item.Amount <= 0)
                    continue;
                var key = GetSuppliesPoolKey(item);
                held.TryGetValue(key, out var count);
                held[key] = count + item.Amount;
            }
        }

        if (leftRemainder != null)
        {
            foreach (var (itemId, modifierId, amount) in leftRemainder)
            {
                if (amount < 0)
                {
                    error = $"left remainder of {amount} x {itemId} is negative";
                    return false;
                }
                if (amount == 0)
                    continue;
                var key = GetSuppliesPoolKey(itemId, modifierId);
                held.TryGetValue(key, out var count);
                held[key] = count + amount;
            }
        }

        foreach (var pair in held)
        {
            pool.TryGetValue(pair.Key, out var allowed);
            if (pair.Value > allowed)
            {
                error = $"take plus remainder of {pair.Value} x {pair.Key} exceeds authorized pool of {allowed}";
                return false;
            }
        }

        return true;
    }

    // Full commit shape for a volunteers loot screen. The pool troop is the only
    // thing of value on the left; everything else handed through this screen
    // (prisoners taken/recruited, gold, influence, morale, donations, prisoner
    // actions) must be untouched, otherwise the commit is rejected. Returning
    // troops to the pool (positive left delta up to the pool size) is allowed.
    internal static bool TryValidateVolunteersCommit(
        string troopId,
        int troopCount,
        TroopRosterData rightMemberDelta,
        TroopRosterData leftMemberDelta,
        TroopRosterData leftPrisonerDelta,
        TroopRosterData rightPrisonerDelta,
        int takenPrisonerCount,
        int recruitedPrisonerCount,
        int goldChange,
        int influenceChange,
        int moraleChange,
        bool applyReleasedAndTakenActions,
        string donationSettlementId,
        IEnumerable<(string fromId, string toId, int number)> upgradedTroops,
        out string error)
    {
        if (!TryValidateVolunteersTake(troopId, troopCount, rightMemberDelta, out error, upgradedTroops))
            return false;

        // Left final must stay within the pool: final = pool + delta per troop.
        if (leftMemberDelta.Data != null)
        {
            foreach (var element in leftMemberDelta.Data)
            {
                var initial = element.CharacterId == troopId ? troopCount : 0;
                var final = initial + element.Number;
                if (final < 0 || final > Math.Max(initial, 0))
                {
                    error = $"left remainder of {final} x {element.CharacterId} is outside the authorized pool of {initial}";
                    return false;
                }
            }
        }

        if (HasNonZeroDelta(leftPrisonerDelta))
        {
            error = "left prisoner delta is not empty";
            return false;
        }

        if (HasPositiveDelta(rightPrisonerDelta))
        {
            error = "right prisoner gain is not empty";
            return false;
        }

        if (takenPrisonerCount > 0 || recruitedPrisonerCount > 0)
        {
            error = "prisoner take/recruit is not empty";
            return false;
        }

        if (goldChange != 0 || influenceChange != 0 || moraleChange != 0)
        {
            error = "gold/influence/morale change is not zero";
            return false;
        }

        if (applyReleasedAndTakenActions || donationSettlementId != null)
        {
            error = "prisoner actions/donation are not empty";
            return false;
        }

        error = null;
        return true;
    }

    private static bool HasNonZeroDelta(TroopRosterData delta)
    {
        if (delta.Data == null)
            return false;
        foreach (var element in delta.Data)
        {
            if (element.Number != 0)
                return true;
        }

        return false;
    }

    private static bool HasPositiveDelta(TroopRosterData delta)
    {
        if (delta.Data == null)
            return false;
        foreach (var element in delta.Data)
        {
            if (element.Number > 0)
                return true;
        }

        return false;
    }

    private static string GetSuppliesPoolKey(ItemRosterElementData data)
    {
        return GetSuppliesPoolKey(
            data.ItemObjectData.ItemObjectId,
            data.ItemObjectData.ItemModifierNull ? null : data.ItemObjectData.ItemModifierId);
    }

    private static string GetSuppliesPoolKey(string itemId, string modifierId)
    {
        return itemId + "|" + (modifierId ?? string.Empty);
    }

    private sealed class PendingForceTransfer
    {
        public PendingForceTransfer(ForceTransferPoolData pool, DateTime authorizedAtUtc)
        {
            Pool = pool;
            AuthorizedAtUtc = authorizedAtUtc;
        }

        public ForceTransferPoolData Pool { get; }
        public DateTime AuthorizedAtUtc { get; }
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
            if (pair.Value.SettlementId != settlementId || !pair.Value.IsExpired)
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
            if (pair.Value.SettlementId == settlementId && !pair.Value.IsExpired)
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
