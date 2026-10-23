namespace GameInterface.Services.Issues.Framework.Interface;

public interface IQuestFeatureFlag
{
    // Off everywhere except nightly and local Debug builds
    bool IsEnabled { get; }
}
