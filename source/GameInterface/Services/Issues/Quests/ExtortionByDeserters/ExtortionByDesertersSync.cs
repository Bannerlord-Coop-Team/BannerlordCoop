using GameInterface.AutoSync;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.ExtortionByDeserters.Patches;

namespace GameInterface.Services.Issues.Quests.ExtortionByDeserters;

internal sealed class ExtortionByDesertersSync : IAutoSync
{
    public ExtortionByDesertersSync(IAutoSyncPatchCollector collector, IQuestFeatureFlag questFeatureFlag)
    {
        if (!questFeatureFlag.IsEnabled) return;

        // Install these after AutoSync so their callers cannot inline unpatched setters.
        ExtortionByDesertersPartyPatches.AddPatches(collector);
        ExtortionByDesertersJournalPatches.AddPatches(collector);
        ExtortionByDesertersServerMethodsPatch.AddPatches(collector);
    }
}
