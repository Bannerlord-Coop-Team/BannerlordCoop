using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
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
    public readonly CampaignVec2 PlayerPartyPosition;
    [ProtoMember(4)]
    public readonly EndCaptivityDetail Detail;
    [ProtoMember(5)]
    public readonly string FacilitatorId;
    [ProtoMember(6)]
    public readonly int RansomAmount;

    public NetworkEndPlayerCaptivityAttempted(
        string playerHeroId,
        string playerPartyId,
        CampaignVec2 playerPartyPosition,
        EndCaptivityDetail detail,
        string facilitatorId,
        int ransomAmount)
    {
        PlayerHeroId = playerHeroId;
        PlayerPartyId = playerPartyId;
        PlayerPartyPosition = playerPartyPosition;
        Detail = detail;
        FacilitatorId = facilitatorId;
        RansomAmount = ransomAmount;
    }
}