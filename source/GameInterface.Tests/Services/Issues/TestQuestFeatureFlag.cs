using GameInterface.Services.Issues.Framework.Interface;

namespace GameInterface.Tests.Services.Issues;

public class TestQuestFeatureFlag : IQuestFeatureFlag
{
    public static readonly TestQuestFeatureFlag On = new(true);
    public static readonly TestQuestFeatureFlag Off = new(false);

    public bool IsEnabled { get; }

    public TestQuestFeatureFlag(bool isEnabled)
    {
        IsEnabled = isEnabled;
    }
}
