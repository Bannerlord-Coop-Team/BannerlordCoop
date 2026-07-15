using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.HeroDevelopers.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkUpdateRosterVersionAfterPerkActivation : ICommand
{
    [ProtoMember(1)]
    public readonly string MemberRosterId;

    public NetworkUpdateRosterVersionAfterPerkActivation(string memberRosterId)
    {
        MemberRosterId = memberRosterId;
    }
}
