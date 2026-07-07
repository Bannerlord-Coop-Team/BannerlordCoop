using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestRaidAiInterventionConfigChange : ICommand
{
    [ProtoMember(1)]
    public readonly bool Allow;

    public NetworkRequestRaidAiInterventionConfigChange(bool allow)
    {
        Allow = allow;
    }
}
