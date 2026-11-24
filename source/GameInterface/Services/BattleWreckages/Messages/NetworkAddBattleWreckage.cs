using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.BattleWreckages.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkAddBattleWreckage : ICommand
{
    [ProtoMember(1)]
    public readonly string BattleWreckageId;

    public NetworkAddBattleWreckage(string battleWreckageId)
    {
        BattleWreckageId = battleWreckageId;
    }
}
