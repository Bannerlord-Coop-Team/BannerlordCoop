using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.BesiegerCamps.Messages;

[ProtoContract(SkipConstructor = true)]
internal class NetworkChangeBesiegerCampSiegeEvent : ICommand
{
    public NetworkChangeBesiegerCampSiegeEvent(string besiegerCampId, string siegeEventId)
    {
        BesiegerCampId = besiegerCampId;
        SiegeEventId = siegeEventId;
    }

    [ProtoMember(1)]
    public string BesiegerCampId { get; }
    [ProtoMember(2)]
    public string SiegeEventId { get; }
}
