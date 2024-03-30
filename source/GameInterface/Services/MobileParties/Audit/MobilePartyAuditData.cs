using ProtoBuf;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Audit;

[ProtoContract(SkipConstructor = true)]
internal record MobilePartyAuditData : IAuditInfo
{
    [ProtoMember(1)]
    public string StringId { get; }

    [ProtoMember(2)]
    public string Name { get; }

    public MobilePartyAuditData(MobileParty party)
    {
        StringId = party.StringId;
        Name = party.Name?.ToString();
    }
}
