using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.PlayerCaptivityService.Messages;

/// <summary>
/// Requests that the server release a hero on behalf of a client action.
/// </summary>
[ProtoContract]
internal readonly struct NetworkEndCaptivityAttempted : ICommand
{
    [ProtoMember(1)]
    public readonly string PrisonerId;
    [ProtoMember(2)]
    public readonly EndCaptivityDetail Detail;
    [ProtoMember(3)]
    public readonly string FacilitatorId;
    [ProtoMember(4)]
    public readonly bool ShowNotification;

    public NetworkEndCaptivityAttempted(string prisonerId, EndCaptivityDetail detail, string facilitatorId, bool showNotification)
    {
        PrisonerId = prisonerId;
        Detail = detail;
        FacilitatorId = facilitatorId;
        ShowNotification = showNotification;
    }
}
