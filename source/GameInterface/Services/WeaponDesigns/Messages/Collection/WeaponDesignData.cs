namespace GameInterface.Services.WeaponDesigns.Messages.Collection
{
    public record WeaponDesignData
    {
        public string CraftingId { get; }
        public string WeaponDesignId { get; }

        public WeaponDesignData(string craftingId, string weaponDesignId)
        {
            CraftingId = craftingId;
            WeaponDesignId = weaponDesignId;
        }
    }
}