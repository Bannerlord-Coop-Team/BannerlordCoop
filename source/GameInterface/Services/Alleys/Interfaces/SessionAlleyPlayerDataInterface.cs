using Common.Logging;
using GameInterface.CoopSessionData;
using GameInterface.Services.TroopRosters.Data;
using Serilog;
using System.Collections.Generic;

namespace GameInterface.Services.Alleys.Interfaces;

/// <summary>
/// Server-authoritative access to the per-alley management state (garrison + overseer) stored in
/// the CoopSession. The vanilla AlleyCampaignBehavior keeps this in its <c>_playerOwnedCommonAreaData</c>
/// list, which only exists on the owning client; the host (no main hero) keeps the canonical copy here
/// so it is saved and transferred to joining clients. All data is keyed by the alley's network id.
/// </summary>
public interface ISessionAlleyPlayerDataInterface : IGameAbstraction
{
    bool TryGetManagementData(string alleyId, out AlleyManagementData data);
    void SetManagementData(string alleyId, string overseerId, TroopRosterElementData[] garrison);
    void RemoveManagementData(string alleyId);
}

/// <inheritdoc cref="ISessionAlleyPlayerDataInterface"/>
public class SessionAlleyPlayerDataInterface : ISessionAlleyPlayerDataInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<SessionAlleyPlayerDataInterface>();

    private readonly ICoopSessionProvider coopSessionProvider;

    private Dictionary<string, AlleyManagementData> ManagementData
        => coopSessionProvider.CoopSession?.AlleyPlayerData?.ManagementDataPerAlley;

    public SessionAlleyPlayerDataInterface(ICoopSessionProvider coopSessionProvider)
    {
        this.coopSessionProvider = coopSessionProvider;
    }

    public bool TryGetManagementData(string alleyId, out AlleyManagementData data)
    {
        data = null;
        var map = ManagementData;
        if (map == null) return false;
        return map.TryGetValue(alleyId, out data);
    }

    public void SetManagementData(string alleyId, string overseerId, TroopRosterElementData[] garrison)
    {
        // Callers (the server request handlers and the set_owner cheat) already run on the game thread.
        var map = ManagementData;
        if (map == null)
        {
            Logger.Error("AlleyPlayerData was null; cannot store management data for {alleyId}", alleyId);
            return;
        }

        map[alleyId] = new AlleyManagementData(overseerId, garrison ?? new TroopRosterElementData[0]);
    }

    public void RemoveManagementData(string alleyId)
    {
        ManagementData?.Remove(alleyId);
    }
}
