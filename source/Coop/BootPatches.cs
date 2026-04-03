using HarmonyLib;
using Serilog;
using System;
using System.IO;
using System.Reflection;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;

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

        // FontFactory.DefaultFont returns TaleWorlds.TwoDimension.Font, which is not a direct reference.
        // Use reflection to check it without requiring that assembly reference.
        private static readonly PropertyInfo _defaultFontProp =
            typeof(FontFactory).GetProperty("DefaultFont", BindingFlags.Public | BindingFlags.Instance);

        private static bool HasValidDefaultFont(FontFactory ff)
        {
            if (ff == null) return false;
            try { return _defaultFontProp?.GetValue(ff) != null; }
            catch { return false; }
        }

        private static bool _applied = false;

        internal static void Apply()
        {
            if (_applied) return;
            _applied = true;
            try
            {
                FileLog("Apply() called");

                var harmony = new Harmony("Coop.BootFix");

                // ROOT CAUSE FIX: postfix on Font.TryLoadFontFromPath.
                // When both DebugAutoConnect processes race to open the same .bfnt file,
                // File.Open uses FileShare.None — the losing process gets IOException,
                // TryLoadFontFromPath returns false, and the font is never added to _bitmapFonts.
                // Later, GetFont("FiraSansExtraCondensed-Light") falls back to DefaultFont.get(),
                // CurrentLanguage is null mid-parse → NullReferenceException.
                // This postfix retries LoadFromPathAux after a brief sleep, by which time the
                // other process has closed the file.
                var fontType = typeof(FontFactory)
                    .GetProperty("DefaultFont", BindingFlags.Public | BindingFlags.Instance)
                    ?.PropertyType;
                var tryLoadFontFromPath = fontType?.GetMethod("TryLoadFontFromPath",
                    BindingFlags.Public | BindingFlags.Instance);
                FileLog($"Font.TryLoadFontFromPath found: {tryLoadFontFromPath != null}");
                if (tryLoadFontFromPath != null)
                {
                    var tlfpPostfix = typeof(BootPatches).GetMethod(nameof(Font_TryLoadFontFromPath_Postfix), BindingFlags.Static | BindingFlags.NonPublic);
                    harmony.Patch(tryLoadFontFromPath, postfix: new HarmonyMethod(tlfpPostfix));
                    FileLog("Patched Font.TryLoadFontFromPath with retry postfix");
                }

                // DEFENSE IN DEPTH: prefix on FontFactory.DefaultFont.get().
                // If TryLoadFontFromPath still fails after retries, CurrentLanguage will be null
                // mid-parse. This prefix returns null instead of crashing, so VS never sees
                // a first-chance exception. HasValidDefaultFont detects the null downstream.
                var defaultFontGetter = typeof(FontFactory)
                    .GetProperty("DefaultFont", BindingFlags.Public | BindingFlags.Instance)
                    ?.GetGetMethod();
                FileLog($"FontFactory.DefaultFont.get found: {defaultFontGetter != null}");
                if (defaultFontGetter != null)
                {
                    var dfPrefix = typeof(BootPatches).GetMethod(nameof(FontFactory_DefaultFont_get_Prefix), BindingFlags.Static | BindingFlags.NonPublic);
                    harmony.Patch(defaultFontGetter, prefix: new HarmonyMethod(dfPrefix));
                    FileLog("Patched FontFactory.DefaultFont.get with null-guard prefix");
                }

                // PRIMARY FIX: prefix on the private LoadMovieAux(string, ViewModel) method.
                //
                // The GauntletLayer that crashes is created during GauntletUISubModule.OnSubModuleLoad(),
                // which runs BEFORE CoopMod.OnSubModuleLoad() (dependency order). At that point our patch
                // is not yet active, so the ctor postfix cannot help. The null FontFactory is baked into
                // UIContext at construction and never fixed afterward.
                //
                // LoadMovieAux is called just before UIContext is actually used for widget creation, so
                // patching it lets us detect and fix a null FontFactory on the exact layer instance that
                // is about to crash — regardless of when that layer was constructed.
                var loadMovieAux = typeof(GauntletLayer).GetMethod(
                    "LoadMovieAux",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                FileLog($"LoadMovieAux found: {loadMovieAux != null}");

                if (loadMovieAux != null)
                {
                    var prefix = typeof(BootPatches).GetMethod(nameof(LoadMovieAux_EnsureResources), BindingFlags.Static | BindingFlags.NonPublic);
                    harmony.Patch(loadMovieAux, prefix: new HarmonyMethod(prefix));
                    FileLog("Patched LoadMovieAux successfully");
                    Logger.Information("[BootPatches] GauntletLayer.LoadMovieAux patched to guard null FontFactory");
                }
                else
                {
                    // Fallback: patch the ctor. Only covers layers created after this Apply() call.
                    var ctor = AccessTools.Constructor(typeof(GauntletLayer), new[] { typeof(string), typeof(int), typeof(bool) });
                    FileLog($"LoadMovieAux not found — falling back to ctor patch. Ctor found: {ctor != null}");
                    if (ctor != null)
                    {
                        var postfix = typeof(BootPatches).GetMethod(nameof(GauntletLayer_ctor_Postfix), BindingFlags.Static | BindingFlags.NonPublic);
                        harmony.Patch(ctor, postfix: new HarmonyMethod(postfix));
                        FileLog("Patched GauntletLayer ctor as fallback");
                    }
                }
            }
            catch (Exception ex)
            {
                FileLog($"Apply() threw: {ex}");
                Logger.Error(ex, "[BootPatches] Apply() failed");
            }
        }

        // Prefix on FontFactory.DefaultFont.get() — null-guard.
        // Returns null (via ref object __result) when CurrentLanguage is null so that
        // Language.CreateFrom stores null in DefaultFont rather than crashing.
        private static bool FontFactory_DefaultFont_get_Prefix(FontFactory __instance, ref object __result)
        {
            try
            {
                var currentLang = typeof(FontFactory)
                    .GetProperty("CurrentLanguage", BindingFlags.Public | BindingFlags.Instance)
                    ?.GetValue(__instance);
                if (currentLang != null) return true; // safe — let original run
                __result = null;
                FileLog("FontFactory.DefaultFont.get: CurrentLanguage null — returning null to prevent NPE");
                return false; // skip original
            }
            catch { return true; }
        }

        // Postfix on Font.TryLoadFontFromPath — retries on failure.
        private static void Font_TryLoadFontFromPath_Postfix(object __instance, string path, ref bool __result)
        {
            if (__result) return;
            try
            {
                var loadAux = __instance.GetType()
                    .GetMethod("LoadFromPathAux", BindingFlags.NonPublic | BindingFlags.Instance);
                if (loadAux == null) { FileLog($"Font_TryLoadFontFromPath_Postfix: LoadFromPathAux not found for {path}"); return; }

                var spriteData = typeof(UIResourceManager)
                    .GetProperty("SpriteData", BindingFlags.Public | BindingFlags.Static)
                    ?.GetValue(null);

                for (int i = 0; i < 3; i++)
                {
                    System.Threading.Thread.Sleep(50 * (i + 1));
                    try
                    {
                        loadAux.Invoke(__instance, new object[] { path, spriteData });
                        __result = true;
                        FileLog($"Font_TryLoadFontFromPath: retry {i + 1} succeeded for {System.IO.Path.GetFileName(path)}");
                        return;
                    }
                    catch (TargetInvocationException tie)
                    {
                        FileLog($"Font_TryLoadFontFromPath: retry {i + 1} failed for {System.IO.Path.GetFileName(path)}: {tie.InnerException?.GetType().Name}");
                    }
                }
                FileLog($"Font_TryLoadFontFromPath: all retries exhausted for {System.IO.Path.GetFileName(path)}");
            }
            catch (Exception ex) { FileLog($"Font_TryLoadFontFromPath_Postfix threw: {ex.GetType().Name}"); }
        }

        // Prefix on GauntletLayer.LoadMovieAux(string, ViewModel).
        // Fires just before UIContext is handed to GauntletMovie.Load — the exact moment
        // a null FontFactory would cause TextWidget..ctor to crash.
        // Fixes the UIContext in-place by forcing a full resource refresh and re-running
        // InitializeContext() on this layer via the engine's own refresh-end path.
        private static void LoadMovieAux_EnsureResources(GauntletLayer __instance, string movieName)
        {
            try
            {
                var ff = __instance.UIContext?.FontFactory;
                FileLog($"LoadMovieAux prefix: movie={movieName} FontFactory={ff != null} DefaultFont={HasValidDefaultFont(ff)}");

                // Guard covers three cases:
                //  1. UIContext is null
                //  2. FontFactory is null (original bug — captured before UIResourceManager.Refresh() ran)
                //  3. FontFactory.DefaultFont is null — FontFactory object exists but LoadAllFonts found no
                //     language XML files (empty _fontLanguageMap → CurrentLanguage returns null → DefaultFont NPE)
                //  DefaultFont checked via reflection to avoid requiring TaleWorlds.TwoDimension assembly reference.
                if (HasValidDefaultFont(ff)) return;

                Logger.Warning("[BootPatches] LoadMovieAux called with null/empty FontFactory on layer — forcing Refresh()");
                FileLog($"FontFactory={ff != null} DefaultFont={HasValidDefaultFont(ff)} — calling UIResourceManager.Refresh()");

                UIResourceManager.Refresh();
                FileLog($"Refresh() done — FontFactory null: {UIResourceManager.FontFactory == null}");

                // Rebuild this layer's UIContext using the now-initialised resources.
                var onRefreshEnd = typeof(GauntletLayer).GetMethod("OnResourceRefreshEnd", BindingFlags.Public | BindingFlags.Instance);
                if (onRefreshEnd != null)
                {
                    onRefreshEnd.Invoke(__instance, new object[] { new System.Collections.Generic.List<GauntletMovieIdentifier>() });
                    FileLog("OnResourceRefreshEnd called — UIContext rebuilt");
                    Logger.Information("[BootPatches] UIContext rebuilt with valid FontFactory before LoadMovieAux");
                }
                else
                {
                    FileLog("OnResourceRefreshEnd not found — UIContext not rebuilt");
                    Logger.Warning("[BootPatches] OnResourceRefreshEnd not found — crash may still occur");
                }
            }
            catch (Exception ex)
            {
                FileLog($"LoadMovieAux_EnsureResources threw: {ex}");
                Logger.Error(ex, "[BootPatches] LoadMovieAux prefix failed");
            }
        }

        // Fallback postfix on GauntletLayer(string, int, bool) ctor — used only when LoadMovieAux
        // is not found. Covers layers created after Apply() runs; cannot help layers created before.
        private static void GauntletLayer_ctor_Postfix(GauntletLayer __instance)
        {
            try
            {
                var ff = __instance.UIContext?.FontFactory;
                FileLog($"GauntletLayer ctor postfix: FontFactory={ff != null} DefaultFont={HasValidDefaultFont(ff)}");
                if (HasValidDefaultFont(ff)) return;

                Logger.Warning("[BootPatches] GauntletLayer ctor: null FontFactory — forcing Refresh()");
                UIResourceManager.Refresh();

                var onRefreshEnd = typeof(GauntletLayer).GetMethod("OnResourceRefreshEnd", BindingFlags.Public | BindingFlags.Instance);
                if (onRefreshEnd != null)
                {
                    onRefreshEnd.Invoke(__instance, new object[] { new System.Collections.Generic.List<GauntletMovieIdentifier>() });
                    FileLog("GauntletLayer ctor postfix: UIContext rebuilt");
                }
            }
            catch (Exception ex)
            {
                FileLog($"GauntletLayer_ctor_Postfix threw: {ex}");
                Logger.Error(ex, "[BootPatches] Ctor postfix failed");
            }
        }
    }
}
