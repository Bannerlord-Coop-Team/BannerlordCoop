#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;

namespace Missions.Agents.Handlers;

public partial class AgentMovementHandler
{
    private Func<CoopAgentInfo, bool> navalStationEligibility;
    private readonly Dictionary<RecipientMovementState, Dictionary<Guid, NavalStationMovementCounts>> navalStationMovement = new();

    public void ConfigureNavalStationMovement(Func<CoopAgentInfo, bool> eligibility)
    {
        navalStationEligibility = eligibility;
        navalStationMovement.Clear();
    }

    public object InspectNavalStationMovement() => new
    {
        enabled = navalStationEligibility != null,
        sampledUtcTicks = DateTime.UtcNow.Ticks,
        rows = navalStationMovement.Values.SelectMany(rows => rows.Values).ToArray(),
        measurement = "Per-recipient cadence-admitted captures and successful send callbacks, not receive or native target observations."
    };

    private sealed class NavalStationMovementCounts
    {
        public string Recipient;
        public Guid CombatantId;
        public string OriginalOwner;
        public long Captured;
        public long Withheld;
        public long Sent;
        public long EligibleSent;
        public bool LastEligible;
        public long LastAdmissionUtcTicks;
    }

    private bool WithholdNavalStationMovement(string controllerId, RecipientMovementState recipient, CapturedMovement captured)
    {
        if (navalStationEligibility == null) return false;
        bool eligible = !captured.IsMount && !captured.IsPriority && captured.AgentData.MountData == null
            && navalStationEligibility(captured.AgentInfo);
        // The fixture has two participants and ten fixed actors; diagnostics never grow beyond that inventory.
        if (!navalStationMovement.TryGetValue(recipient, out var rows) && navalStationMovement.Count < 2)
        {
            rows = new Dictionary<Guid, NavalStationMovementCounts>();
            navalStationMovement.Add(recipient, rows);
        }
        if (rows != null)
        {
            if (!rows.TryGetValue(captured.AgentInfo.AgentId, out var counts) && rows.Count < 10)
            {
                counts = new NavalStationMovementCounts
                {
                    Recipient = controllerId, CombatantId = captured.AgentInfo.AgentId,
                    OriginalOwner = captured.AgentInfo.OriginalOwner
                };
                rows.Add(counts.CombatantId, counts);
            }
            if (counts != null)
            {
                counts.Captured++;
                counts.LastEligible = eligible;
                counts.LastAdmissionUtcTicks = DateTime.UtcNow.Ticks;
                if (eligible) counts.Withheld++;
            }
        }
        if (!eligible) return false;
        // A withheld pose is neither sent nor deferred; exit must compare against no baseline.
        recipient.LastSentMovement.Remove(captured.AgentInfo.AgentId);
        recipient.MovementPendingSince.Remove(captured.AgentInfo.AgentId);
        return true;
    }

    private void RecordNavalStationMovementSent(RecipientMovementState recipient, Guid agentId)
    {
        if (!navalStationMovement.TryGetValue(recipient, out var rows) || !rows.TryGetValue(agentId, out var counts)) return;
        counts.Sent++;
        if (counts.LastEligible) counts.EligibleSent++;
    }
}
#endif
