using Common.Network;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.MapEvents.TroopSupply.Messages;
using System.Collections.Generic;

namespace Missions.Battles;

/// <summary>
/// Throttled supply-progress reporting (game thread, via the mission tick): tells the server how far each of
/// our troop suppliers has spawned, so its ledger pointer advances and a new owner can resume from it on
/// disconnect/migration. Only sends when a count changed.
/// </summary>
public interface ISupplyProgressReporter
{
    /// <summary>[Game thread] Advance the report timer; reports at most once per interval.</summary>
    void Tick(float dt);
}

/// <inheritdoc cref="ISupplyProgressReporter"/>
public class SupplyProgressReporter : ISupplyProgressReporter
{
    private const float SupplyReportInterval = 1f;

    private readonly INetwork relayNetwork;
    private readonly IBattleSession session;

    private float reportTimer;
    private readonly Dictionary<string, int> lastReport = new();

    public SupplyProgressReporter(INetwork relayNetwork, IBattleSession session)
    {
        this.relayNetwork = relayNetwork;
        this.session = session;
    }

    public void Tick(float dt)
    {
        reportTimer += dt;
        if (reportTimer < SupplyReportInterval) return;
        reportTimer = 0f;
        ReportSupplyProgress();
    }

    // [Owner, game thread] Report how far each of our suppliers has spawned so the server's ledger pointer
    // advances; a new owner is then resumed from it on disconnect/migration. Only owned parties have entries
    // (a non-owned side's supplier is empty), and we skip the send when nothing changed.
    private void ReportSupplyProgress()
    {
        if (!session.HasInstance) return;

        var entries = new List<SupplyProgressEntry>();
        bool changed = false;
        foreach (var supplier in CoopTroopSupplierRegistry.GetSuppliers(session.InstanceId))
        {
            foreach (var (partyId, supplied) in supplier.GetSuppliedByParty())
            {
                entries.Add(new SupplyProgressEntry(partyId, supplied));
                if (!lastReport.TryGetValue(partyId, out var last) || last != supplied)
                {
                    changed = true;
                    lastReport[partyId] = supplied;
                }
            }
        }

        if (!changed || entries.Count == 0) return;
        relayNetwork.SendAll(new NetworkBattleSupplyProgress(session.InstanceId, entries.ToArray()));
    }
}
