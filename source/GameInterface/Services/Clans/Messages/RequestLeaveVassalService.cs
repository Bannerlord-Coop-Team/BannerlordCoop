using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Clans.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct RequestLeaveVassalService : ICommand
{
    [ProtoMember(1)]
    public readonly string ClanId;

    public RequestLeaveVassalService(string clanId)
    {
        ClanId = clanId;
    }
}
