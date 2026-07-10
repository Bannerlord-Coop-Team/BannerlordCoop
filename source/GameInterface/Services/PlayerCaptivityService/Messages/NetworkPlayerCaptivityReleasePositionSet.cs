using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.PlayerCaptivityService.Messages;

[ProtoContract]
internal readonly struct NetworkPlayerCaptivityReleasePositionSet : IEvent
{
    [ProtoMember(1)]
    public readonly string PlayerPartyId;

    [ProtoMember(2)]
    public readonly CampaignVec2 ReleasePosition;

    public NetworkPlayerCaptivityReleasePositionSet(string playerPartyId, CampaignVec2 releasePosition)
    {
        PlayerPartyId = playerPartyId;
        ReleasePosition = releasePosition;
    }
}
