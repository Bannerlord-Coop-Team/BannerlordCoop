using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Kingdoms.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkStartRebellion : ICommand
{
    [ProtoMember(1)]
    public readonly string ClanId;

    public NetworkStartRebellion(string clanId)
    {
        ClanId = clanId;
    }
}
