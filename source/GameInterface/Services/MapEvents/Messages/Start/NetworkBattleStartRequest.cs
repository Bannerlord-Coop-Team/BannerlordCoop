using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Start;

/// <summary>
/// [Client -&gt; Server] Asks the server to start the battle for a map event in a given mode (0 = live mission,
/// 1 = auto-resolve simulation; see <c>BattleStartMode</c>). The owning mode handler gates it against
/// <c>ServerBattleModeArbiter</c>, sets the battle up if accepted, and answers with <see cref="NetworkBattleStartReply"/>.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkBattleStartRequest : ICommand
{
    [ProtoMember(1)]
    public readonly string RequestId;

    [ProtoMember(2)]
    public readonly int Mode;

    [ProtoMember(3)]
    public readonly string MapEventId;

    [ProtoMember(4)]
    public readonly string AttackerPartyId;

    public NetworkBattleStartRequest(string requestId, int mode, string mapEventId, string attackerPartyId)
    {
        RequestId = requestId;
        Mode = mode;
        MapEventId = mapEventId;
        AttackerPartyId = attackerPartyId;
    }
}
