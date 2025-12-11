using ProtoBuf;

namespace GameInterface.Services.ItemRosters.Data
{
    [ProtoContract(SkipConstructor = true)]
    public class ItemRosterOwner
    {
        [ProtoMember(1)]
        public string ItemRosterId { get; set; }

        [ProtoMember(2)]
        public string OwnerPartyId { get; set; }

        public ItemRosterOwner(string itemRosterId, string ownerPartyId)
        {
            ItemRosterId = itemRosterId;
            OwnerPartyId = ownerPartyId;
        }
    }
}
