using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Stances.Data;

/// <summary>
/// Data required for updating a faction in a StanceLink
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record StanceLinkFactionChangedData
{
    public StanceLinkFactionChangedData(string _StanceId,  string _FactionId, bool _IsFaction1)
    {
        StanceId = _StanceId;
        FactionId = _FactionId;
        IsFaction1 = _IsFaction1;
    }

    [ProtoMember(1)]
    public string StanceId { get; }
    [ProtoMember(2)]
    public string FactionId { get; }
    [ProtoMember(3)]
    public bool IsFaction1 { get; }
}
