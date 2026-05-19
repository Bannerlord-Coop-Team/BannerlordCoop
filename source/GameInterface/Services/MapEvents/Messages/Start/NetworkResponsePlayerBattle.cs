using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Start;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkResponsePlayerBattle : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventString;

    public NetworkResponsePlayerBattle(string mapEventString)
    {
        MapEventString = mapEventString;
    }
}