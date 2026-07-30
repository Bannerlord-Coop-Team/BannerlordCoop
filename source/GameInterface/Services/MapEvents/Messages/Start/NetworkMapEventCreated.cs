using Common.Messaging;
using GameInterface.Services.MapEvents;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Start;

/// <summary>
/// Server -&gt; Client response carrying the object-manager id of the authoritatively created
/// <see cref="TaleWorlds.CampaignSystem.MapEvents.MapEvent"/>, correlated to the original request by <see cref="RequestId"/>.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkMapEventCreated : ICommand
{
    [ProtoMember(1)]
    public readonly string RequestId;
    [ProtoMember(2)]
    public readonly MapEventCreationOutcome Outcome;
    [ProtoMember(3)]
    public readonly string MapEventId;

    public NetworkMapEventCreated(
        string requestId,
        MapEventCreationOutcome outcome,
        string mapEventId)
    {
        RequestId = requestId;
        Outcome = outcome;
        MapEventId = mapEventId;
    }
}
