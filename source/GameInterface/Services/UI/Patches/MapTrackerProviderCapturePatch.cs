using HarmonyLib;
using SandBox.ViewModelCollection.Map.Tracker;
namespace GameInterface.Services.UI.Patches;

[HarmonyPatch(typeof(MapTrackerProvider))]
[HarmonyPatch(MethodType.Constructor)]
internal static class MapTrackerProviderCapturePatch
{
    [HarmonyPostfix]
    static void Postfix(MapTrackerProvider __instance)
    {
        if (ContainerProvider.TryResolve<IMapTrackerProviderHolder>(out var holder))
        {
            holder.Current = __instance;
        }
    }
}
internal interface IMapTrackerProviderHolder
{
    MapTrackerProvider Current { get; set; }
}

internal class MapTrackerProviderHolder : IMapTrackerProviderHolder
{
    public MapTrackerProvider Current { get; set; }
}