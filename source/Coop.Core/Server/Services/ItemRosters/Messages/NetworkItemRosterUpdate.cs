using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.ItemRosters.Messages;

/// <summary>
/// Sent to the client by the server when an ItemRoster is updated.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkItemRosterUpdate : IMessage
{
    [ProtoMember(1)]
    public readonly string ItemRosterId;

    [ProtoMember(2)]
    public readonly string ItemID;

    [ProtoMember(3)]
    public readonly string ItemModifierID;

    [ProtoMember(4)]
    public readonly int Amount;

    public NetworkItemRosterUpdate(string itemRosterId, string itemID, string itemModifierID, int amount)
    {
        ItemRosterId = itemRosterId;
        ItemID = itemID;
        ItemModifierID = itemModifierID;
        Amount = amount;
    }
}
