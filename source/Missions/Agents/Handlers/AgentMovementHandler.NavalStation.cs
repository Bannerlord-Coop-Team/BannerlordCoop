#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Missions.Agents.Packets;

namespace Missions.Agents.Handlers;

public partial class AgentMovementHandler
{
    private Func<CoopAgentInfo, bool> navalStationEligibility;
    private Func<CoopAgentInfo, bool> navalHelmEligibility;
    private Func<CoopAgentInfo, long?> navalHelmRevision;
    private Func<CoopAgentInfo, long, bool> acceptNavalHelmMovement;
    private readonly Dictionary<RecipientMovementState, Dictionary<Guid, NavalStationMovementCounts>> navalStationMovement = new();

    public void ConfigureNavalStationMovement(Func<CoopAgentInfo, bool> eligibility, Func<CoopAgentInfo, bool> helmEligibility = null,
        Func<CoopAgentInfo, long?> helmRevision = null, Func<CoopAgentInfo, long, bool> acceptHelmMovement = null)
    {
        navalStationEligibility = eligibility;
        navalHelmEligibility = helmEligibility;
        navalHelmRevision = helmRevision;
        acceptNavalHelmMovement = acceptHelmMovement;
        navalStationMovement.Clear();
    }

    private void StampNavalHelmMovement(string scope, ushort[] compactIds, Guid[] canonicalIds, AgentData[] data)
    {
        if (navalHelmRevision == null) return;
        for (int i = 0; i < data.Length; i++)
        {
            CoopAgentInfo info;
            bool found = scope == null ? agentRegistry.TryGetAgentInfo(canonicalIds[i], out info)
                : agentRegistry.TryGetAgentInfo(scope, compactIds[i], out info);
            // Stamp at packet construction, not capture; unrelated actors always keep the default field.
            data[i].NavalHelmRevision = found ? navalHelmRevision(info) ?? 0 : 0;
        }
    }

    public object InspectNavalStationMovement() => new
    {
        enabled = navalStationEligibility != null || navalHelmEligibility != null,
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
        if (navalStationEligibility == null && navalHelmEligibility == null) return false;
        bool eligible = !captured.IsMount && captured.AgentData.MountData == null
            && (captured.IsPriority ? navalHelmEligibility?.Invoke(captured.AgentInfo) == true
                || navalHelmRevision?.Invoke(captured.AgentInfo) < 0
                : navalStationEligibility?.Invoke(captured.AgentInfo) == true);
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
