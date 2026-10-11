using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.HeroDevelopers.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkUpdateItemRosterVersionAfterPerkChange : ICommand
{
    [ProtoMember(1)]
    public readonly string ItemRosterId;

    public NetworkUpdateItemRosterVersionAfterPerkChange(string itemRosterId)
    {
        ItemRosterId = itemRosterId;
    }
}
