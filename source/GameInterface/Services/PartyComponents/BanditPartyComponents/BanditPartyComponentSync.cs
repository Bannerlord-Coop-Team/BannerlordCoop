using GameInterface.AutoSync;

namespace GameInterface.Services.PartyComponents.BanditPartyComponents
{
    internal class BanditPartyComponentSync : IAutoSync
    {
        public BanditPartyComponentSync(AutoSyncRegistry autoSyncBuilder)
        {
            //autoSyncBuilder.AddProperty(AccessTools.Property(typeof(CaravanPartyComponent), nameof(CaravanPartyComponent.Settlement)));

            //autoSyncBuilder.AddField(AccessTools.Field(typeof(BanditPartyComponent), nameof(BanditPartyComponent.Clan)));
        }
    }
}