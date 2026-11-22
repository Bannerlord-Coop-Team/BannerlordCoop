using GameInterface.AutoSync;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.ExtortionByDeserters.Patches;

namespace GameInterface.Services.Issues.Quests.ExtortionByDeserters;

internal sealed class ExtortionByDesertersSync : IAutoSync
{
    public ExtortionByDesertersSync(IAutoSyncPatchCollector collector, IQuestFeatureFlag questFeatureFlag)
    {
        if (!questFeatureFlag.IsEnabled) return;

        // Queue these prefixes after AutoSync's setter patches so the patched bodies cannot inline unsynced setters.
        ExtortionByDesertersServerMethodsPatch.AddPatches(collector);
    }
}
