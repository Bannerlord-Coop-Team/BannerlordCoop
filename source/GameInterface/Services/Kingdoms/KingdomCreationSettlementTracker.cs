using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Kingdoms;

public interface IKingdomCreationSettlementTracker
{
    void Track(string partyId, string settlementId);
    void Reset();
    void Clear(MobileParty party, string partyId);
    void TrackParty(MobileParty party, string partyId, Settlement settlement, string settlementId);
    void Complete(string partyId);
    bool TryConsumeLeave(string partyId);
    bool TryConsumeLeave(MobileParty party, string partyId);
    bool TryGetTrackedSettlement(MobileParty party, out Settlement settlement);
}

public class KingdomCreationSettlementTracker : IKingdomCreationSettlementTracker
{
    private static readonly TimeSpan LeaveSuppressionWindow = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan CompletedLeaveSuppressionWindow = TimeSpan.FromSeconds(1);
    private readonly object gate = new();
    private readonly Dictionary<string, PendingSettlement> pendingSettlements = new();
    private ConditionalWeakTable<MobileParty, PendingSettlement> pendingParties = new();

    public void Track(string partyId, string settlementId)
    {
        if (string.IsNullOrWhiteSpace(partyId) || string.IsNullOrWhiteSpace(settlementId)) return;

        lock (gate)
        {
            TrackNoLock(partyId, new PendingSettlement(settlementId, settlementId, null, DateTime.UtcNow));
        }
    }

    public void Reset()
    {
        lock (gate)
        {
            pendingSettlements.Clear();
            pendingParties = new ConditionalWeakTable<MobileParty, PendingSettlement>();
        }
    }

    public void Clear(MobileParty party, string partyId)
    {
        lock (gate)
        {
            if (party != null)
            {
                pendingParties.Remove(party);
                if (!string.IsNullOrWhiteSpace(party.StringId))
                {
                    pendingSettlements.Remove(party.StringId);
                }
            }

            if (!string.IsNullOrWhiteSpace(partyId))
            {
                pendingSettlements.Remove(partyId);
            }
        }
    }

    public void TrackParty(MobileParty party, string partyId, Settlement settlement, string settlementId)
    {
        if (party == null || settlement == null) return;

        string resolvedPartyId = string.IsNullOrWhiteSpace(partyId) ? party.StringId : partyId;
        string resolvedSettlementId = string.IsNullOrWhiteSpace(settlementId) ? settlement.StringId : settlementId;
        if (string.IsNullOrWhiteSpace(resolvedPartyId) || string.IsNullOrWhiteSpace(resolvedSettlementId)) return;

        var pending = new PendingSettlement(resolvedSettlementId, settlement.StringId, settlement, DateTime.UtcNow);

        lock (gate)
        {
            TrackNoLock(resolvedPartyId, pending);
            TrackNoLock(party.StringId, pending);
            pendingParties.Remove(party);
            pendingParties.Add(party, pending);
        }
    }

    public void Complete(string partyId)
    {
        if (string.IsNullOrWhiteSpace(partyId)) return;

        lock (gate)
        {
            RemoveExpiredNoLock();
            if (!pendingSettlements.TryGetValue(partyId, out var pending)) return;

            pending.IsComplete = true;
            pending.UpdatedAt = DateTime.UtcNow;
            pending.RemainingCompletedLeaveSuppressions = 2;
            pending.RemainingCompletedSettlementProtections = 1;
        }
    }

    public bool TryConsumeLeave(string partyId)
    {
        if (string.IsNullOrWhiteSpace(partyId)) return false;

        lock (gate)
        {
            RemoveExpiredNoLock();
            return pendingSettlements.TryGetValue(partyId, out var pending) && TryConsumeLeaveNoLock(pending);
        }
    }

    public bool TryConsumeLeave(MobileParty party, string partyId)
    {
        lock (gate)
        {
            RemoveExpiredNoLock();
            if (party != null &&
                pendingParties.TryGetValue(party, out var pending) &&
                TryConsumeLeaveNoLock(pending))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(partyId) &&
                pendingSettlements.TryGetValue(partyId, out pending) &&
                TryConsumeLeaveNoLock(pending))
            {
                return true;
            }

            if (party != null &&
                !string.IsNullOrWhiteSpace(party.StringId) &&
                pendingSettlements.TryGetValue(party.StringId, out pending) &&
                TryConsumeLeaveNoLock(pending))
            {
                return true;
            }

            return false;
        }
    }

    public bool TryGetTrackedSettlement(MobileParty party, out Settlement settlement)
    {
        settlement = null;
        if (party == null) return false;

        lock (gate)
        {
            RemoveExpiredNoLock();
            if (!TryGetPendingNoLock(party, out var pending)) return false;
            if (IsExpired(pending)) return false;

            settlement = ResolveSettlement(pending);
            if (settlement == null) return false;
            if (!pending.IsComplete)
            {
                pendingParties.Remove(party);
                RemovePendingNoLock(pending);
                return true;
            }
            if (pending.RemainingCompletedSettlementProtections <= 0)
            {
                pendingParties.Remove(party);
                RemovePendingNoLock(pending);
                return false;
            }

            pending.RemainingCompletedSettlementProtections--;
            if (pending.RemainingCompletedSettlementProtections <= 0)
            {
                pendingParties.Remove(party);
                RemovePendingNoLock(pending);
            }

            return true;
        }
    }

    private void TrackNoLock(string partyId, PendingSettlement pending)
    {
        if (string.IsNullOrWhiteSpace(partyId)) return;

        pendingSettlements[partyId] = pending;
    }

    private bool TryGetPendingNoLock(MobileParty party, out PendingSettlement pending)
    {
        if (pendingParties.TryGetValue(party, out pending))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(party.StringId) &&
            pendingSettlements.TryGetValue(party.StringId, out pending))
        {
            return true;
        }

        pending = default;
        return false;
    }

    private bool TryConsumeLeaveNoLock(PendingSettlement pending)
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

    private void RemovePendingNoLock(PendingSettlement pending)
    {
        foreach (string partyId in pendingSettlements
                     .Where(pair => ReferenceEquals(pair.Value, pending))
                     .Select(pair => pair.Key)
                     .ToList())
        {
            pendingSettlements.Remove(partyId);
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

    private void RemoveExpiredNoLock()
    {
        foreach (string partyId in pendingSettlements
                     .Where(pair => IsExpired(pair.Value))
                     .Select(pair => pair.Key)
                     .ToList())
        {
            pendingSettlements.Remove(partyId);
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
