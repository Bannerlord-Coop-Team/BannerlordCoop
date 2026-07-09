using GameInterface.AutoSync;

namespace GameInterface.Services.SiegeEnginesConstructionProgress
{
    internal class SiegeEngineConstructionProgressSync : IAutoSync
    {
        public SiegeEngineConstructionProgressSync(AutoSyncRegistry autoSyncBuilder)
        {
            // Nothing on a siege engine uses the generic per-set property sync. Progress/RedeploymentProgress
            // use the threshold sync in SiegeEngineProgressPatches, and Hitpoints/MaxHitPoints now replicate
            // there too (register-then-broadcast, applied on the game thread): the generic property sync dropped
            // values set before an engine had a network id (the constructor's initial hitpoints, and battle
            // damage that raced the engine's late registration). RangedSiegeEngine is server-only bombardment
            // state never read on clients, and the readonly SiegeEngine field rides on the deploy/reserve type id.
        }
    }
}
