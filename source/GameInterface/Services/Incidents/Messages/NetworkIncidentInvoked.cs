using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Incidents.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkIncidentInvoked : ICommand
{
    [ProtoMember(1)]
    public readonly string IncidentId;

    [ProtoMember(2)]
    public readonly long ActiveIncidentSeed;

    public NetworkIncidentInvoked(string incidentId, long activeIncidentSeed)
    {
        IncidentId = incidentId;
        ActiveIncidentSeed = activeIncidentSeed;
    }
}
