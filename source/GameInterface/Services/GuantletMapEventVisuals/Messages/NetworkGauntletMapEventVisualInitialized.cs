using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.GuantletMapEventVisuals.Messages;

[ProtoContract]
internal readonly struct NetworkGauntletMapEventVisualInitialized : IEvent
{
    [ProtoMember(1)]
    public readonly string InstanceId;
    [ProtoMember(2)]
    public readonly CampaignVec2 Position;
    [ProtoMember(3)]
    public readonly bool IsVisible;

    public NetworkGauntletMapEventVisualInitialized(string instanceId, CampaignVec2 position, bool isVisible)
    {
        InstanceId = instanceId;
        Position = position;
        IsVisible = isVisible;
    }
}
