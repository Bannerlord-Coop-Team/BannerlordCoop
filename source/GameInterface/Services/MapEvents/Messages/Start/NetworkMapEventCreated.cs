using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Start;

/// <summary>
/// Server -> Client response carrying the object-manager id of the authoritatively created
/// <see cref="TaleWorlds.CampaignSystem.MapEvents.MapEvent"/>, correlated to the original request by <see cref="RequestId"/>.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkMapEventCreated : ICommand
{
    [ProtoMember(1)]
    public readonly string RequestId;
    [ProtoMember(2)]
    public readonly string MapEventId;

    public NetworkMapEventCreated(string requestId, string mapEventId)
    {
        RequestId = requestId;
        MapEventId = mapEventId;
    }
}
