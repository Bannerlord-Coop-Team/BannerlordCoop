using GameInterface.AutoSync;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.ExtortionByDeserters.Patches;

namespace GameInterface.Services.Issues.Quests.ExtortionByDeserters;

internal sealed class ExtortionByDesertersSync : IAutoSync
{
    public ExtortionByDesertersSync(IAutoSyncPatchCollector collector, IQuestFeatureFlag questFeatureFlag)
    {
        if (!questFeatureFlag.IsEnabled) return;

        // These methods need AutoSync installed before their patched bodies can inline game setters.
        ExtortionByDesertersServerMethodsPatch.AddPatches(collector);
    }
}
