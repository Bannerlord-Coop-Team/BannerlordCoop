using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.PartyComponents.BanditPartyComponents.Messages;

[ProtoContract]
internal readonly struct NetworkUpdateBanditPartyComponentInitArgs : IEvent
{
    [ProtoMember(1)]
    public readonly string BanditPartyComponentId;
    [ProtoMember(2)]
    public readonly string ClanId;
    [ProtoMember(3)]
    public readonly CampaignVec2 InitialPosition;
    [ProtoMember(4)]
    public readonly string PartyTemplateId;

    public NetworkUpdateBanditPartyComponentInitArgs(
        string banditPartyComponentId,
        string clanId,
        CampaignVec2 initialPosition,
        string partyTemplateId)
    {
        BanditPartyComponentId = banditPartyComponentId;
        ClanId = clanId;
        InitialPosition = initialPosition;
        PartyTemplateId = partyTemplateId;
    }
}
