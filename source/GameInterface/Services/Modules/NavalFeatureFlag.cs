namespace GameInterface.Services.Modules;

/// <summary>
/// Whether this build turns on War Sails (NavalDLC) co-op at all, like <c>IQuestFeatureFlag</c> does for quests.
/// </summary>
public interface INavalFeatureFlag
{
    // Off everywhere except nightly and local Debug builds
    bool IsEnabled { get; }
}

/// <summary>
/// War Sails co-op only runs in builds compiled with <c>TESTER</c>, which the nightly workflow and local Debug builds define.
/// </summary>
public class NavalFeatureFlag : INavalFeatureFlag
{
#if TESTER
    public bool IsEnabled => true;
#else
    public bool IsEnabled => false;
#endif
}
