using HarmonyLib;
using Serilog;
using System;
using System.IO;
using System.Reflection;
using TaleWorlds.Engine.GauntletUI;

namespace Coop
{
    internal static class BootPatches
    {
        // Static classes can't be used as a generic type argument, so use Log.ForContext(Type) directly.
        // Property (not field) ensures we pick up the logger after Serilog is configured.
        private static ILogger Logger => Serilog.Log.ForContext(typeof(BootPatches));

        // Fallback file log in case Serilog isn't ready yet.
        private static void FileLog(string message)
        {
            try { File.AppendAllText("BootPatches.log", $"{DateTime.Now:HH:mm:ss.fff} {message}\n"); }
            catch { }
        }

        internal static void Apply()
        {
            try
            {
                FileLog("Apply() called");

                var refreshFontFactory = typeof(UIResourceManager)
                    .GetMethod("RefreshFontFactory", BindingFlags.NonPublic | BindingFlags.Static);
                FileLog($"RefreshFontFactory found: {refreshFontFactory != null}");

                var harmony = new Harmony("Coop.BootFix");

                // Patch GauntletLayer constructor — it calls InitializeContext() internally,
                // so a postfix on the ctor lets us verify and fix FontFactory after the fact.
                // We use AccessTools so Harmony resolves the right overload safely.
                var ctor = AccessTools.Constructor(typeof(GauntletLayer), new[] { typeof(string), typeof(int), typeof(bool) });
                FileLog($"GauntletLayer ctor found: {ctor != null}");

                if (ctor == null)
                {
                    FileLog("GauntletLayer ctor not found — trying InitializeContext");
                    var initCtx = typeof(GauntletLayer).GetMethod("InitializeContext", BindingFlags.NonPublic | BindingFlags.Instance);
                    FileLog($"InitializeContext found: {initCtx != null}");
                    if (initCtx != null)
                    {
                        var prefix = typeof(BootPatches).GetMethod(nameof(InitializeContext_EnsureResources), BindingFlags.Static | BindingFlags.NonPublic);
                        harmony.Patch(initCtx, prefix: new HarmonyMethod(prefix));
                        FileLog("Patched InitializeContext as fallback");
                    }
                    return;
                }

                var postfix = typeof(BootPatches).GetMethod(nameof(GauntletLayer_ctor_Postfix), BindingFlags.Static | BindingFlags.NonPublic);
                harmony.Patch(ctor, postfix: new HarmonyMethod(postfix));
                FileLog("Patched GauntletLayer ctor successfully");

                Logger.Information("[BootPatches] GauntletLayer ctor patched to guard null FontFactory");
            }
            catch (Exception ex)
            {
                FileLog($"Apply() threw: {ex}");
                Logger.Error(ex, "[BootPatches] Apply() failed");
            }
        }

        // Postfix on GauntletLayer(string, int, bool) — fires after the ctor (and its InitializeContext call).
        // If UIContext.FontFactory ended up null, we force a full resource refresh and rebuild the context.
        private static void GauntletLayer_ctor_Postfix(GauntletLayer __instance)
        {
            try
            {
                if (__instance.UIContext?.FontFactory != null) return;

                FileLog($"GauntletLayer ctor postfix: FontFactory null — UIContext={__instance.UIContext != null}");
                Logger.Warning("[BootPatches] GauntletLayer constructed with null FontFactory — forcing UIResourceManager.Refresh()");

                UIResourceManager.Refresh();
                FileLog($"Refresh() done — FontFactory null: {UIResourceManager.FontFactory == null}");

                // Trigger OnResourceRefreshEnd to rebuild the UIContext with the now-valid factories.
                // This re-calls InitializeContext() with non-null FontFactory.
                var onRefreshEnd = typeof(GauntletLayer).GetMethod("OnResourceRefreshEnd", BindingFlags.Public | BindingFlags.Instance);
                if (onRefreshEnd != null)
                {
                    // OnResourceRefreshEnd(List<GauntletMovieIdentifier>) — pass empty list since no movies are loaded yet.
                    onRefreshEnd.Invoke(__instance, new object[] { new System.Collections.Generic.List<GauntletMovieIdentifier>() });
                    FileLog("OnResourceRefreshEnd called — UIContext rebuilt");
                    Logger.Information("[BootPatches] UIContext rebuilt with valid FontFactory");
                }
                else
                {
                    FileLog("OnResourceRefreshEnd not found");
                    Logger.Warning("[BootPatches] OnResourceRefreshEnd not found — UIContext may still have null FontFactory");
                }
            }
            catch (Exception ex)
            {
                FileLog($"GauntletLayer_ctor_Postfix threw: {ex}");
                Logger.Error(ex, "[BootPatches] Postfix failed");
            }
        }

        // Fallback prefix for InitializeContext (used if ctor patch isn't found).
        private static void InitializeContext_EnsureResources()
        {
            if (UIResourceManager.FontFactory != null) return;

            FileLog("InitializeContext prefix: FontFactory null — calling Refresh()");
            Logger.Warning("[BootPatches] FontFactory null before InitializeContext — calling Refresh()");
            try
            {
                UIResourceManager.Refresh();
                FileLog($"Refresh() done — FontFactory null: {UIResourceManager.FontFactory == null}");
            }
            catch (Exception ex)
            {
                FileLog($"Refresh() threw: {ex}");
                Logger.Error(ex, "[BootPatches] Refresh() failed in InitializeContext prefix");
            }
        }
    }
}
