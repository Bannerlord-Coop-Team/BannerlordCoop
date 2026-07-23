using GameInterface.Services.TroopRosters.Data;
using ProtoBuf;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Alleys;

/// <summary>
/// The per-alley management state (garrison + overseer) that AlleyCampaignBehavior keeps in its
/// <c>_playerOwnedCommonAreaData</c> list. That list only exists on the machine whose main hero
/// owns the alley, and in co-op the host has no main hero, so the authoritative copy lives here in
/// the CoopSession (saved and transferred to joining clients), keyed by the alley's network id.
/// The alley's owner itself is not stored here; it lives on the Alley and is synced separately.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class AlleyPlayerData
{
    // Keyed by Alley network id -> management data for that alley.
    [ProtoMember(1)]
    public Dictionary<string, AlleyManagementData> ManagementDataPerAlley { get; }

    public AlleyPlayerData(Dictionary<string, AlleyManagementData> managementDataPerAlley)
    {
        ManagementDataPerAlley = managementDataPerAlley;
    }
}

/// <summary>
/// The garrison roster and overseer hero for a single player-owned alley. Troops and the overseer
/// are stored by registry id so they resolve identically on every machine.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class AlleyManagementData
{
    [ProtoMember(1)]
    public string OverseerId { get; set; }

    [ProtoMember(2)]
    public TroopRosterElementData[] Garrison { get; set; }

    // Set while a rival gang is attacking this alley (the AI-initiated defense flow): the attacking
    // alley's network id and the deadline to answer. Null/default when not under attack. Stored here so
    // the attack survives a save and reaches a client that joins mid-attack.
    [ProtoMember(3)]
    public string UnderAttackByAlleyId { get; set; }

    [ProtoMember(4)]
    public CampaignTime AttackResponseDueDate { get; set; }

    public AlleyManagementData(string overseerId, TroopRosterElementData[] garrison)
    {
        OverseerId = overseerId;
        Garrison = garrison;
    }
}
