using Common.Messaging;
using Coop.Naval.Storms.Data;
using ProtoBuf;

namespace Coop.Naval.Storms.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkStormStateChanged : ICommand
{
    [ProtoMember(1)]
    public readonly string StormId;

    [ProtoMember(2)]
    public readonly StormStateData State;

    [ProtoMember(3)]
    public readonly bool IsNewStorm;

    public NetworkStormStateChanged(string stormId, StormStateData state, bool isNewStorm)
    {
        StormId = stormId;
        State = state;
        IsNewStorm = isNewStorm;
    }
}
