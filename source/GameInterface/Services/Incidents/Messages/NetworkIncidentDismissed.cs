using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Incidents.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkIncidentDismissed : ICommand
{
    [ProtoMember(1)]
    public readonly string IncidentId;

    public NetworkIncidentDismissed(string incidentId)
    {
        IncidentId = incidentId;
    }
}
