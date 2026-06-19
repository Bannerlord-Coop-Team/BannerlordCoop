using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Kingdoms;

public static class KingdomCreationSettlementTracker
{
    private static readonly TimeSpan LeaveSuppressionWindow = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan CompletedLeaveSuppressionWindow = TimeSpan.FromSeconds(1);
    private static readonly object Gate = new();
    private static readonly Dictionary<string, PendingSettlement> PendingSettlements = new();
    private static ConditionalWeakTable<MobileParty, PendingSettlement> PendingParties = new();

    public static void Track(string partyId, string settlementId)
    {
        if (string.IsNullOrWhiteSpace(partyId) || string.IsNullOrWhiteSpace(settlementId)) return;

        lock (Gate)
        {
            TrackNoLock(partyId, new PendingSettlement(settlementId, settlementId, null, DateTime.UtcNow));
        }
    }

    public static void Reset()
    {
        lock (Gate)
        {
            PendingSettlements.Clear();
            PendingParties = new ConditionalWeakTable<MobileParty, PendingSettlement>();
        }
    }

    public static void Clear(MobileParty party, string partyId)
    {
        lock (Gate)
        {
            if (party != null)
            {
                PendingParties.Remove(party);
                if (!string.IsNullOrWhiteSpace(party.StringId))
                {
                    PendingSettlements.Remove(party.StringId);
                }
            }

            if (!string.IsNullOrWhiteSpace(partyId))
            {
                PendingSettlements.Remove(partyId);
            }
        }
    }

    public static void TrackParty(MobileParty party, string partyId, Settlement settlement, string settlementId)
    {
        if (party == null || settlement == null) return;

        string resolvedPartyId = string.IsNullOrWhiteSpace(partyId) ? party.StringId : partyId;
        string resolvedSettlementId = string.IsNullOrWhiteSpace(settlementId) ? settlement.StringId : settlementId;
        if (string.IsNullOrWhiteSpace(resolvedPartyId) || string.IsNullOrWhiteSpace(resolvedSettlementId)) return;

        var pending = new PendingSettlement(resolvedSettlementId, settlement.StringId, settlement, DateTime.UtcNow);

        lock (Gate)
        {
            TrackNoLock(resolvedPartyId, pending);
            TrackNoLock(party.StringId, pending);
            PendingParties.Remove(party);
            PendingParties.Add(party, pending);
        }
    }

    public static void Complete(string partyId)
    {
        if (string.IsNullOrWhiteSpace(partyId)) return;

        lock (Gate)
        {
            RemoveExpiredNoLock();
            if (!PendingSettlements.TryGetValue(partyId, out var pending)) return;

            pending.IsComplete = true;
            pending.UpdatedAt = DateTime.UtcNow;
            pending.RemainingCompletedLeaveSuppressions = 2;
            pending.RemainingCompletedSettlementProtections = 1;
        }
    }

    public static bool TryConsumeLeave(string partyId)
    {
        if (string.IsNullOrWhiteSpace(partyId)) return false;

        lock (Gate)
        {
            RemoveExpiredNoLock();
            return PendingSettlements.TryGetValue(partyId, out var pending) && TryConsumeLeaveNoLock(pending);
        }
    }

    public static bool TryConsumeLeave(MobileParty party, string partyId)
    {
        lock (Gate)
        {
            RemoveExpiredNoLock();
            if (party != null &&
                PendingParties.TryGetValue(party, out var pending) &&
                TryConsumeLeaveNoLock(pending))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(partyId) &&
                PendingSettlements.TryGetValue(partyId, out pending) &&
                TryConsumeLeaveNoLock(pending))
            {
                return true;
            }

            if (party != null &&
                !string.IsNullOrWhiteSpace(party.StringId) &&
                PendingSettlements.TryGetValue(party.StringId, out pending) &&
                TryConsumeLeaveNoLock(pending))
            {
                return true;
            }

            return false;
        }
    }

    public static bool TryGetTrackedSettlement(MobileParty party, out Settlement settlement)
    {
        settlement = null;
        if (party == null) return false;

        lock (Gate)
        {
            RemoveExpiredNoLock();
            if (!TryGetPendingNoLock(party, out var pending)) return false;
            if (IsExpired(pending)) return false;

            settlement = ResolveSettlement(pending);
            if (settlement == null) return false;
            if (!pending.IsComplete)
            {
                PendingParties.Remove(party);
                RemovePendingNoLock(pending);
                return true;
            }
            if (pending.RemainingCompletedSettlementProtections <= 0)
            {
                PendingParties.Remove(party);
                RemovePendingNoLock(pending);
                return false;
            }

            pending.RemainingCompletedSettlementProtections--;
            if (pending.RemainingCompletedSettlementProtections <= 0)
            {
                PendingParties.Remove(party);
                RemovePendingNoLock(pending);
            }

            return true;
        }
    }

    private static void TrackNoLock(string partyId, PendingSettlement pending)
    {
        if (string.IsNullOrWhiteSpace(partyId)) return;

        PendingSettlements[partyId] = pending;
    }

    private static bool TryGetPendingNoLock(MobileParty party, out PendingSettlement pending)
    {
        if (PendingParties.TryGetValue(party, out pending))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(party.StringId) &&
            PendingSettlements.TryGetValue(party.StringId, out pending))
        {
            return true;
        }

        pending = default;
        return false;
    }

    private static bool TryConsumeLeaveNoLock(PendingSettlement pending)
    {
        if (IsExpired(pending)) return false;
        if (!pending.IsComplete) return true;

        if (pending.RemainingCompletedLeaveSuppressions <= 0) return false;

        pending.RemainingCompletedLeaveSuppressions--;
        if (pending.RemainingCompletedLeaveSuppressions <= 0 &&
            pending.RemainingCompletedSettlementProtections <= 0)
        {
            RemovePendingNoLock(pending);
        }

        return true;
    }

    private static void RemovePendingNoLock(PendingSettlement pending)
    {
        foreach (string partyId in PendingSettlements
                     .Where(pair => ReferenceEquals(pair.Value, pending))
                     .Select(pair => pair.Key)
                     .ToList())
        {
            PendingSettlements.Remove(partyId);
        }
    }

    private static Settlement ResolveSettlement(PendingSettlement pending)
    {
        if (pending.Settlement != null)
        {
            return pending.Settlement;
        }

        if (Settlement.All == null) return null;

        return Settlement.All.FirstOrDefault(settlement =>
            settlement != null &&
            (settlement.StringId == pending.SettlementId ||
             settlement.StringId == pending.SettlementStringId));
    }

    private static void RemoveExpiredNoLock()
    {
        foreach (string partyId in PendingSettlements
                     .Where(pair => IsExpired(pair.Value))
                     .Select(pair => pair.Key)
                     .ToList())
        {
            PendingSettlements.Remove(partyId);
        }
    }

    private static bool IsExpired(PendingSettlement pending)
    {
        TimeSpan window = pending?.IsComplete == true ? CompletedLeaveSuppressionWindow : LeaveSuppressionWindow;

        return pending == null ||
               DateTime.UtcNow - pending.UpdatedAt > window;
    }

    private class PendingSettlement
    {
        public readonly string SettlementId;
        public readonly string SettlementStringId;
        public readonly Settlement Settlement;
        public DateTime UpdatedAt;
        public bool IsComplete;
        public int RemainingCompletedLeaveSuppressions;
        public int RemainingCompletedSettlementProtections;

        public PendingSettlement(string settlementId, string settlementStringId, Settlement settlement, DateTime updatedAt)
        {
            SettlementId = settlementId;
            SettlementStringId = settlementStringId;
            Settlement = settlement;
            UpdatedAt = updatedAt;
        }
    }
}
