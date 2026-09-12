using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.ItemRosters.Messages;

/// <summary>
/// Sent to the client by the server when an ItemRoster is updated.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkItemRosterUpdate : IMessage, IJoinCatchUpMergeMessage
{
    [ProtoMember(1)]
    public readonly string ItemRosterId;

    [ProtoMember(2)]
    public readonly string ItemID;

    [ProtoMember(3)]
    public readonly string ItemModifierID;

    [ProtoMember(4)]
    public readonly int Amount;

    public string JoinCatchUpMergeKey => $"{ItemRosterId}:{ItemID}:{ItemModifierID}";

    public bool TryMerge(IMessage next, out IMessage merged)
    {
        merged = null;
        if (next is not NetworkItemRosterUpdate other ||
            ItemRosterId != other.ItemRosterId || ItemID != other.ItemID ||
            ItemModifierID != other.ItemModifierID ||
            Amount <= 0 || other.Amount <= 0)
            return false;

        // Native removals can throw on underflow after updating caches. Preserve each removal
        // and every sign change instead of combining their partial effects.
        long total = (long)Amount + other.Amount;
        if (total < int.MinValue || total > int.MaxValue) return false;

        merged = new NetworkItemRosterUpdate(ItemRosterId, ItemID, ItemModifierID, (int)total);
        return true;
    }

    public NetworkItemRosterUpdate(string itemRosterId, string itemID, string itemModifierID, int amount)
    {
        ItemRosterId = itemRosterId;
        ItemID = itemID;
        ItemModifierID = itemModifierID;
        Amount = amount;
    }
}
