using GameInterface.Services.Armies.Extensions;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Armies.Data;

/// <summary>
/// Data required for creating an Army
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record ArmyCreationData
{
    public ArmyCreationData(string stringId, string kingdomId, string leaderPartyId, short armyType)
    {
        StringId = stringId;
        KingdomId = kingdomId;
        LeaderPartyId = leaderPartyId;
        ArmyType = armyType;
    }

    [ProtoMember(1)]
    public string StringId { get; }
    [ProtoMember(2)]
    public string KingdomId { get; }
    [ProtoMember(3)]
    public string LeaderPartyId { get; }
    [ProtoMember(4)]
    public short ArmyType { get; }

}
