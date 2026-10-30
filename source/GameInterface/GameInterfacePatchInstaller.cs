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
    /// Installs the detour guard, the static fixes, every uncategorized patch and the enabled quest and naval
    /// categories onto <paramref name="harmony"/>.
    /// </summary>
    void Install(Harmony harmony);
}

/// <inheritdoc cref="IGameInterfacePatchInstaller"/>
internal class GameInterfacePatchInstaller : IGameInterfacePatchInstaller
{
    private readonly IQuestFeatureFlag questFeatureFlag;
    private readonly bool navalPatchesEnabled;

    // GameInterfaceModule passes whether NavalDLC co-op is active, so the naval category is applied only then.
    public GameInterfacePatchInstaller(IQuestFeatureFlag questFeatureFlag, bool navalPatchesEnabled)
    {
        this.questFeatureFlag = questFeatureFlag;
        this.navalPatchesEnabled = navalPatchesEnabled;
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

        if (navalPatchesEnabled)
        {
            harmony.PatchCategory(assembly, GameInterface.HARMONY_NAVAL_CATEGORY);
        }
    }
}
