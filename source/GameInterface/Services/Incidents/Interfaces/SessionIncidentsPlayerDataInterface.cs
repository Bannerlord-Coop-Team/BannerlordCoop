using GameInterface.CoopSessionData;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Incidents.Interfaces;

public interface ISessionIncidentsPlayerDataInterface : IGameAbstraction
{
    bool IsOnGlobalCooldown(string playerHeroId);
    void SetGlobalCooldown(string playerHeroId, CampaignTime time);
    void AddIncidentOnCooldown(string playerHeroId, string incidentId, CampaignTime cooldownEndTime);
    void ClearIncidentCooldowns(string playerHeroId);
    void CheckIncidentsOnCooldown(string playerHeroId);
    bool IsIncidentOnCooldown(string playerHeroId, string incidentId);
    void SetCanInvokeSettlementEvent(string playerHeroId, bool canInvoke);
    bool CanInvokeSettlementEvent(string playerHeroId);
    void SetActiveIncidentSeed(string playerHeroId, long newSeed);
    long GetActiveIncidentSeed(string playerHeroId);
    void SetPendingIncident(string playerHeroId, string incidentId);
    bool TryConsumePendingIncident(string playerHeroId, string incidentId);
    bool TryGetPendingIncident(string playerHeroId, out string incidentId);
    void ClearPendingIncident(string playerHeroId);
    void AddPlayerKeys(string playerHeroId, CampaignTime initialGlobalCooldown);
}

public class SessionIncidentsPlayerDataInterface : ISessionIncidentsPlayerDataInterface
{
    private readonly ICoopSessionProvider coopSessionProvider;

    private IncidentsPlayerData IncidentsPlayerData => coopSessionProvider.CoopSession.IncidentsPlayerData;

    public SessionIncidentsPlayerDataInterface(ICoopSessionProvider coopSessionProvider)
    {
        this.coopSessionProvider = coopSessionProvider;
    }

    public bool IsOnGlobalCooldown(string playerHeroId)
    {
        // Matches vanilla, which only invokes once _lastGlobalIncidentCooldown.IsPast
        return IncidentsPlayerData.PlayerLastGlobalIncidentCooldown.TryGetValue(playerHeroId, out var cooldownTicks)
            && !new CampaignTime(cooldownTicks).IsPast;
    }

    public void SetGlobalCooldown(string playerHeroId, CampaignTime time)
    {
        IncidentsPlayerData.PlayerLastGlobalIncidentCooldown[playerHeroId] = time.NumTicks;
    }

    public void AddIncidentOnCooldown(string playerHeroId, string incidentId, CampaignTime cooldownEndTime)
    {
        if (!IncidentsPlayerData.PlayerIncidentsOnCooldown.TryGetValue(playerHeroId, out var incidentCooldowns))
        {
            incidentCooldowns = new Dictionary<string, long>();
            IncidentsPlayerData.PlayerIncidentsOnCooldown[playerHeroId] = incidentCooldowns;
        }

        incidentCooldowns[incidentId] = cooldownEndTime.NumTicks;
    }

    public void ClearIncidentCooldowns(string playerHeroId)
    {
        IncidentsPlayerData.PlayerIncidentsOnCooldown.Remove(playerHeroId);
    }

    public void CheckIncidentsOnCooldown(string playerHeroId)
    {
        if (!IncidentsPlayerData.PlayerIncidentsOnCooldown.TryGetValue(playerHeroId, out var incidentCooldowns)) return;

        List<string> expiredIncidentIds = new();
        foreach (KeyValuePair<string, long> incidentCooldown in incidentCooldowns)
        {
            // Same as vanilla's start time + incident.Cooldown <= Now, since the end time is stored instead
            if (new CampaignTime(incidentCooldown.Value) <= CampaignTime.Now)
            {
                expiredIncidentIds.Add(incidentCooldown.Key);
            }
        }

        foreach (string incidentId in expiredIncidentIds)
        {
            incidentCooldowns.Remove(incidentId);
        }
    }

    public bool IsIncidentOnCooldown(string playerHeroId, string incidentId)
    {
        return IncidentsPlayerData.PlayerIncidentsOnCooldown.TryGetValue(playerHeroId, out var incidentCooldowns)
            && incidentCooldowns.ContainsKey(incidentId);
    }

    public void SetCanInvokeSettlementEvent(string playerHeroId, bool canInvoke)
    {
        IncidentsPlayerData.PlayerCanInvokeSettlementEvent[playerHeroId] = canInvoke;
    }

    public bool CanInvokeSettlementEvent(string playerHeroId)
    {
        return IncidentsPlayerData.PlayerCanInvokeSettlementEvent.TryGetValue(playerHeroId, out var canInvoke) && canInvoke;
    }

    public void SetActiveIncidentSeed(string playerHeroId, long newSeed)
    {
        IncidentsPlayerData.PlayerActiveIncidentSeed[playerHeroId] = newSeed;
    }

    public long GetActiveIncidentSeed(string playerHeroId)
    {
        return IncidentsPlayerData.PlayerActiveIncidentSeed.TryGetValue(playerHeroId, out var seed) ? seed : 0L;
    }

    public void SetPendingIncident(string playerHeroId, string incidentId)
    {
        IncidentsPlayerData.PlayerPendingIncident[playerHeroId] = incidentId;
    }

    // Only allow one selected outcome for an incident, repeated replies are ignored
    public bool TryConsumePendingIncident(string playerHeroId, string incidentId)
    {
        if (!IncidentsPlayerData.PlayerPendingIncident.TryGetValue(playerHeroId, out var pendingIncidentId)) return false;
        if (pendingIncidentId != incidentId) return false;

        IncidentsPlayerData.PlayerPendingIncident.Remove(playerHeroId);
        return true;
    }

    public bool TryGetPendingIncident(string playerHeroId, out string incidentId)
    {
        return IncidentsPlayerData.PlayerPendingIncident.TryGetValue(playerHeroId, out incidentId);
    }

    public void ClearPendingIncident(string playerHeroId)
    {
        IncidentsPlayerData.PlayerPendingIncident.Remove(playerHeroId);
    }

    public void AddPlayerKeys(string playerHeroId, CampaignTime initialGlobalCooldown)
    {
        if (IncidentsPlayerData.PlayerLastGlobalIncidentCooldown.ContainsKey(playerHeroId)) return;

        IncidentsPlayerData.PlayerLastGlobalIncidentCooldown[playerHeroId] = initialGlobalCooldown.NumTicks;
    }
}
