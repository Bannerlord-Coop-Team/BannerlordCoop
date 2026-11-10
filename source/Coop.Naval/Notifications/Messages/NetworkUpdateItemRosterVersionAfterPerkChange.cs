using Common.Messaging;
using ProtoBuf;

namespace Coop.Naval.Notifications.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkUpdateItemRosterVersionAfterPerkChange : ICommand
{
    [ProtoMember(1)]
    public readonly string ItemRosterId;

    public NetworkUpdateItemRosterVersionAfterPerkChange(string itemRosterId)
    {
        ItemRosterId = itemRosterId;
    }
}
