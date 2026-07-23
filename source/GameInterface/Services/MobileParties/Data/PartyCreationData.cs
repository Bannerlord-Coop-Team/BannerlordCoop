using ProtoBuf;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Data;

[ProtoContract(SkipConstructor = true)]
public class PartyCreationData
{
    [ProtoMember(1)]
    public string StringId { get; }

    public PartyCreationData(MobileParty party)
    {
        StringId = party.StringId;
    }
}
