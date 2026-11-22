using ProtoBuf;
using System.Collections.Generic;
using TaleWorlds.Core;

namespace GameInterface.Services.OrderOfBattle.Data;

/// <summary>
/// Network and save form for OrderOfBattleCampaignBehavior.OrderOfBattleFormationData
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class OrderOfBattleFormation
{
    [ProtoMember(1)]
    public string CaptainId { get; }

    [ProtoMember(2)]
    public string[] HeroTroopIds { get; }

    [ProtoMember(3)]
    public DeploymentFormationClass FormationClass { get; }

    [ProtoMember(4)]
    public int PrimaryClassWeight { get; }

    [ProtoMember(5)]
    public int SecondaryClassWeight { get; }

    [ProtoMember(6)]
    public Dictionary<FormationFilterType, bool> Filters { get; }

    public OrderOfBattleFormation(
        string captainId,
        string[] heroTroopIds,
        DeploymentFormationClass formationClass,
        int primaryClassWeight,
        int secondaryClassWeight,
        Dictionary<FormationFilterType, bool> filters)
    {
        CaptainId = captainId;
        HeroTroopIds = heroTroopIds;
        FormationClass = formationClass;
        PrimaryClassWeight = primaryClassWeight;
        SecondaryClassWeight = secondaryClassWeight;
        Filters = filters;
    }
}
