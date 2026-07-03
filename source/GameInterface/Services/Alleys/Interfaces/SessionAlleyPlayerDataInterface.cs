using Common.Logging;
using GameInterface.CoopSessionData;
using GameInterface.Services.TroopRosters.Data;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

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

    /// <summary>Records that a rival alley is attacking this player alley, with the answer deadline.</summary>
    void SetUnderAttackByAi(string alleyId, string attackerAlleyId, CampaignTime dueDate);

    /// <summary>Clears the under-attack state once the attack is resolved (defended, lost or timed out).</summary>
    void ClearUnderAttackByAi(string alleyId);
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

        var entry = new AlleyManagementData(overseerId, garrison ?? Array.Empty<TroopRosterElementData>());

        // A garrison/overseer change must not drop an in-progress attack, so carry the under-attack
        // fields forward from the existing entry.
        if (map.TryGetValue(alleyId, out var existing))
        {
            entry.UnderAttackByAlleyId = existing.UnderAttackByAlleyId;
            entry.AttackResponseDueDate = existing.AttackResponseDueDate;
        }

        map[alleyId] = entry;
    }

    public void RemoveManagementData(string alleyId)
    {
        ManagementData?.Remove(alleyId);
    }

    public void SetUnderAttackByAi(string alleyId, string attackerAlleyId, CampaignTime dueDate)
    {
        // Only a managed (player-owned) alley can be under attack; if there's no entry there's nothing to mark.
        if (!TryGetManagementData(alleyId, out var data)) return;
        data.UnderAttackByAlleyId = attackerAlleyId;
        data.AttackResponseDueDate = dueDate;
    }

    public void ClearUnderAttackByAi(string alleyId)
    {
        if (!TryGetManagementData(alleyId, out var data)) return;
        data.UnderAttackByAlleyId = null;
    }
}
