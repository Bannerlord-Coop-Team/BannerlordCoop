using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRaidAiInterventionConfigChanged : ICommand
{
    [ProtoMember(1)]
    public readonly bool Allow;

    public NetworkRaidAiInterventionConfigChanged(bool allow)
    {
        Allow = allow;
    }
}
