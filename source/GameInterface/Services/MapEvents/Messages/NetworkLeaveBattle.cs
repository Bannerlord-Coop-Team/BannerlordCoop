using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkLeaveBattle : ICommand
{
    [ProtoMember(1)]
    public readonly string MobilePartyId;
    [ProtoMember(2)]
    public readonly string MapEventId;

    public NetworkLeaveBattle(string mobilePartyId, string mapEventId)
    {
        MobilePartyId = mobilePartyId;
        MapEventId = mapEventId;
    }
}
