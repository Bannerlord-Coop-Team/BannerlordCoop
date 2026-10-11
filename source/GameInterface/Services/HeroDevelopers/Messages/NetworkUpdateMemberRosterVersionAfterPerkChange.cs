using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.HeroDevelopers.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkUpdateMemberRosterVersionAfterPerkChange : ICommand
{
    [ProtoMember(1)]
    public readonly string MemberRosterId;

    public NetworkUpdateMemberRosterVersionAfterPerkChange(string memberRosterId)
    {
        MemberRosterId = memberRosterId;
    }
}
