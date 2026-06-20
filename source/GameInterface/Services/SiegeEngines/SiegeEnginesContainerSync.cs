using GameInterface.AutoSync;

namespace GameInterface.Services.SiegeEngines
{
    internal class SiegeEnginesContainerSync: IAutoSync
    {
        public SiegeEnginesContainerSync(AutoSyncRegistry autoSyncBuilder)
        {
            // Fields
            //autoSyncBuilder.AddField(AccessTools.Field(typeof(SiegeEnginesContainer), nameof(SiegeEnginesContainer.SiegePreparations)));
        }
    }
}