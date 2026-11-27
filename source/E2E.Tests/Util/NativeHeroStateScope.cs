using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace E2E.Tests.Util;

/// <summary>
/// Lifts the bootstrap stub on <see cref="Hero.ChangeState"/> that skips HeroFieldPatches and events like OnHeroActivated.
/// </summary>
internal sealed class NativeHeroStateScope : IDisposable
{
    private readonly System.Reflection.MethodInfo target = AccessTools.Method(typeof(Hero), nameof(Hero.ChangeState));
    private readonly Patch stub;
    private readonly Harmony harmony;

    public NativeHeroStateScope()
    {
        stub = Assert.Single(Harmony.GetPatchInfo(target).Prefixes,
            patch => patch.PatchMethod.DeclaringType.FullName == "GameInterface.Tests.Bootstrap.Patches.HeroPatches");
        harmony = new Harmony(stub.owner);
        harmony.Unpatch(target, stub.PatchMethod);
    }

    public void Dispose() => harmony.Patch(target, prefix: new HarmonyMethod(stub.PatchMethod)
    {
        priority = stub.priority,
        before = stub.before,
        after = stub.after,
    });
}
