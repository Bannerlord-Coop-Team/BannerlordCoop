using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkAddInvolvedParties : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventId;
    [ProtoMember(2)]
    public readonly string[] MapEventPartyIds;

    public NetworkAddInvolvedParties(string mapEventId, string[] mapEventPartyIds)
    {
        MapEventId = mapEventId;
        MapEventPartyIds = mapEventPartyIds;
    }
}
