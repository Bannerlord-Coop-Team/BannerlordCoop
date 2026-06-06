using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Stances.Data;

/// <summary>
/// Data required for creating a StanceLink
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record StanceLinkCreationData
{
    public StanceLinkCreationData(string _StringId, short _StanceType, string _Faction1Id, string _Faction2Id)
    {
        StringId = _StringId;
        StanceType = _StanceType;
        Faction1Id = _Faction1Id;
        Faction2Id = _Faction2Id;
    }

    [ProtoMember(1)]
    public string StringId { get; }
    [ProtoMember(2)]
    public short StanceType { get; }
    [ProtoMember(3)]
    public string Faction1Id { get; }
    [ProtoMember(4)]
    public string Faction2Id { get; }
}
