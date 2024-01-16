namespace GameInterface.Services.ItemRosters.Messages
{
    public class ItemRosterMessageBase
    {
        public string PartyBaseID { get; }
        public string ItemID { get; }
        public string ItemModifierID { get; }
        public int Amount { get; }

        public ItemRosterMessageBase(string partyBaseID, string itemID, string itemModifierID, int amount)
        {
            PartyBaseID = partyBaseID;
            ItemID = itemID;
            ItemModifierID = itemModifierID;
            Amount = amount;
        }
    }
}
