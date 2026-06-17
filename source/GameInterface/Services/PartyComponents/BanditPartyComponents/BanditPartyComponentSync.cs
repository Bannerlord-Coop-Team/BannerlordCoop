using GameInterface.DynamicSync;

namespace GameInterface.Services.PartyComponents.BanditPartyComponents
{
    internal class BanditPartyComponentSync : IDynamicSync
    {
        public BanditPartyComponentSync(DynamicSyncRegistry autoSyncBuilder)
        {
            //autoSyncBuilder.AddProperty(AccessTools.Property(typeof(CaravanPartyComponent), nameof(CaravanPartyComponent.Settlement)));

            //autoSyncBuilder.AddField(AccessTools.Field(typeof(BanditPartyComponent), nameof(BanditPartyComponent.Clan)));
        }
    }
}