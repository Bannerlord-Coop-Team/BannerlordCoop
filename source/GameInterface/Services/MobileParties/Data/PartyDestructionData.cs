using ProtoBuf;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Data;

[ProtoContract(SkipConstructor = true)]
public record PartyDestructionData
{
    [ProtoMember(1)]
    public string StringId { get; }

    public PartyDestructionData(MobileParty party)
    {
        StringId = party.StringId;
    }
}
