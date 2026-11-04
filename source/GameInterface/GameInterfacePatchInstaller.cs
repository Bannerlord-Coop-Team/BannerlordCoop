using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Utils;
using HarmonyLib;

namespace GameInterface;

/// <summary>
/// Applies the GameInterface assembly's own always-on Harmony patches.
/// </summary>
public interface IGameInterfacePatchInstaller
{
    /// <summary>
    /// Installs the detour guard, the static fixes and every uncategorized patch onto <paramref name="harmony"/>.
    /// </summary>
    void Install(Harmony harmony);
}

/// <inheritdoc cref="IGameInterfacePatchInstaller"/>
internal class GameInterfacePatchInstaller : IGameInterfacePatchInstaller
{
    private readonly IQuestFeatureFlag questFeatureFlag;

    public GameInterfacePatchInstaller(IQuestFeatureFlag questFeatureFlag)
    {
        this.questFeatureFlag = questFeatureFlag;
    }

    /// <inheritdoc/>
    public void Install(Harmony harmony)
    {
        var assembly = typeof(GameInterface).Assembly;

        // Must run before any other detour below, or a fragile no-op method's detour can corrupt its inline
        // x64 unwind info and deadlock the GC.
        FragileDetourGuard.Apply(harmony);

        harmony.PatchCategory(assembly, GameInterface.HARMONY_STATIC_FIXES_CATEGORY);
        harmony.PatchAllUncategorized(assembly);

        if (questFeatureFlag.IsEnabled)
        {
            harmony.PatchCategory(assembly, GameInterface.HARMONY_QUESTS_CATEGORY);
        }
    }
}
