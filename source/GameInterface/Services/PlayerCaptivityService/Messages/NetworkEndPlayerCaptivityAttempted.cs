using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.PlayerCaptivityService.Messages;

[ProtoContract]
internal readonly struct NetworkEndPlayerCaptivityAttempted : IEvent
{
    [ProtoMember(1)]
    public readonly string PlayerHeroId;
    [ProtoMember(2)]
    public readonly string PlayerPartyId;
    [ProtoMember(3)]
    public readonly EndCaptivityDetail Detail;
    [ProtoMember(4)]
    public readonly string FacilitatorId;

    public NetworkEndPlayerCaptivityAttempted(
        string playerHeroId,
        string playerPartyId,
        EndCaptivityDetail detail,
        string facilitatorId)
    {
        PlayerHeroId = playerHeroId;
        PlayerPartyId = playerPartyId;
        Detail = detail;
        FacilitatorId = facilitatorId;
    }
}