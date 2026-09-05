using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.Heroes.HeirSelection.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkClientSelectHeir : ICommand
{
    [ProtoMember(1)]
    public readonly Dictionary<string, int> HeirIdApparents;

    public NetworkClientSelectHeir(Dictionary<string, int> heirIdApparents)
    {
        HeirIdApparents = heirIdApparents;
    }
}