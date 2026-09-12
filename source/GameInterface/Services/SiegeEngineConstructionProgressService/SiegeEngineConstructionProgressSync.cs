using GameInterface.AutoSync;

namespace GameInterface.Services.SiegeEnginesConstructionProgress
{
    internal class SiegeEngineConstructionProgressSync : IAutoSync
    {
        public SiegeEngineConstructionProgressSync(AutoSyncRegistry autoSyncBuilder)
        {
            // Deliberately empty: every siege engine field replicates through SiegeEngineProgressPatches
            // and the deploy/reserve messages, because the generic property sync drops values set before
            // the engine has a network id (initial hitpoints, damage racing its late registration).
        }
    }
}
