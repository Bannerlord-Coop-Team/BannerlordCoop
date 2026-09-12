using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.HeroDevelopers.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkUpdateRosterVersionAfterPerkChange : ICommand
{
    [ProtoMember(1)]
    public readonly string MemberRosterId;

    public NetworkUpdateRosterVersionAfterPerkChange(string memberRosterId)
    {
        MemberRosterId = memberRosterId;
    }
}
