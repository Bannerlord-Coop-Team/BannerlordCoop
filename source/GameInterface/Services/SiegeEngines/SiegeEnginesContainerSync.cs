using GameInterface.DynamicSync;

namespace GameInterface.Services.SiegeEngines
{
    internal class SiegeEnginesContainerSync: IDynamicSync
    {
        public SiegeEnginesContainerSync(DynamicSyncRegistry autoSyncBuilder)
        {
            // Fields
            //autoSyncBuilder.AddField(AccessTools.Field(typeof(SiegeEnginesContainer), nameof(SiegeEnginesContainer.SiegePreparations)));
        }
    }
}