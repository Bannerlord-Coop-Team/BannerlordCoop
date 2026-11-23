using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.BattleWreckages.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkOpenWreckageEncounter : ICommand
{
    [ProtoMember(1)]
    public readonly string BattleWreckageId;

    public NetworkOpenWreckageEncounter(string battleWreckageId)
    {
        BattleWreckageId = battleWreckageId;
    }
}
