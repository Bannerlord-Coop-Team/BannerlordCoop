using Common.Logging;
using HarmonyLib;
using Serilog;
using TaleWorlds.Engine;
using GameInterface.Policies;

namespace GameInterface.Services.GameDebug.Patches
{
    [HarmonyPatch(typeof(VideoPlayerView))]
    internal class VideoPlayerViewPatch
    {
        public static VideoPlayerView CurrentVideoPlayerView;

        [HarmonyPatch(nameof(VideoPlayerView.CreateVideoPlayerView))]
        [HarmonyPostfix]
        private static void CreateVideoPlayerViewPostfix(ref VideoPlayerView __result)
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return;
            CurrentVideoPlayerView = __result;
        }
    }
}
