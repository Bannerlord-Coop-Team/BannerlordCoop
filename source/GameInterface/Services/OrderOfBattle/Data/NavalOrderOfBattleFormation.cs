using ProtoBuf;
using System.Collections.Generic;
using TaleWorlds.Core;

namespace GameInterface.Services.OrderOfBattle.Data;

/// <summary>
/// Network and save form for NavalOrderOfBattleCampaignBehavior.NavalOrderOfBattleFormationData
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NavalOrderOfBattleFormation
{
    [ProtoMember(1)]
    public string CaptainId { get; }

    [ProtoMember(2)]
    public string ShipId { get; }

    [ProtoMember(3)]
    public DeploymentFormationClass FormationClass { get; }

    [ProtoMember(4)]
    public Dictionary<FormationFilterType, bool> Filters { get; }

    public NavalOrderOfBattleFormation(
        string captainId,
        string shipId,
        DeploymentFormationClass formationClass,
        Dictionary<FormationFilterType, bool> filters)
    {
        CaptainId = captainId;
        ShipId = shipId;
        FormationClass = formationClass;
        Filters = filters;
    }
}
