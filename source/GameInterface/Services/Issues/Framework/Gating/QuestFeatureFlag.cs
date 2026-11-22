using GameInterface.Services.Issues.Framework.Interface;

namespace GameInterface.Services.Issues.Framework.Gating;

/// <summary>
/// Quests only run in builds compiled with <c>TESTER</c>, which the nightly workflow and local Debug builds define.
/// </summary>
internal class QuestFeatureFlag : IQuestFeatureFlag
{
#if TESTER
    public bool IsEnabled => true;
#else
    public bool IsEnabled => false;
#endif
}
