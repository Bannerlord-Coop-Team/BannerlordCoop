using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.Incidents;

/// <summary>
/// IncidentsCampaignBehaviour tracks incident cooldowns and the active incident seed for only one player
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class IncidentsPlayerData
{
    // Dictionary<PlayerHeroId, Dictionary<IncidentId, CooldownEndTime CampaignTime._numTicks>>
    [ProtoMember(1)]
    public Dictionary<string, Dictionary<string, long>> PlayerIncidentsOnCooldown { get; }

    // Dictionary<PlayerHeroId, CampaignTime._numTicks>
    [ProtoMember(2)]
    public Dictionary<string, long> PlayerLastGlobalIncidentCooldown { get; }

    // Dictionary<PlayerHeroId, ActiveIncidentSeed>
    [ProtoMember(3)]
    public Dictionary<string, long> PlayerActiveIncidentSeed { get; }

    // Dictionary<PlayerHeroId, CanInvokeSettlementEvent>
    [ProtoMember(4)]
    public Dictionary<string, bool> PlayerCanInvokeSettlementEvent { get; }

    // Dictionary<PlayerHeroId, IncidentId> for the incident sent to the player and not yet resolved
    [ProtoMember(5)]
    public Dictionary<string, string> PlayerPendingIncident { get; }

    public IncidentsPlayerData(
        Dictionary<string, Dictionary<string, long>> playerIncidentsOnCooldown,
        Dictionary<string, long> playerLastGlobalIncidentCooldown,
        Dictionary<string, long> playerActiveIncidentSeed,
        Dictionary<string, bool> playerCanInvokeSettlementEvent,
        Dictionary<string, string> playerPendingIncident)
    {
        PlayerIncidentsOnCooldown = playerIncidentsOnCooldown ?? new();
        PlayerLastGlobalIncidentCooldown = playerLastGlobalIncidentCooldown ?? new();
        PlayerActiveIncidentSeed = playerActiveIncidentSeed ?? new();
        PlayerCanInvokeSettlementEvent = playerCanInvokeSettlementEvent ?? new();
        PlayerPendingIncident = playerPendingIncident ?? new();
    }
}
