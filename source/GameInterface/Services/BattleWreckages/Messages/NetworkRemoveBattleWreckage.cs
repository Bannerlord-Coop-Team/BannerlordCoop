using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.BattleWreckages.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRemoveBattleWreckage : ICommand
{
    [ProtoMember(1)]
    public readonly string BattleWreckageId;

    public NetworkRemoveBattleWreckage(string battleWreckageId)
    {
        BattleWreckageId = battleWreckageId;
    }
}
