# Fix: TextWidget NullRef Crash on Boot

**Branch:** `Devfred/fix-textwidget-nullref-boot`

---

## The Problem

On Bannerlord v1.3.15, launching the game sometimes crashes with a `NullReferenceException` inside `TextWidget..ctor` → `context.FontFactory.get_DefaultFont()`. The crash is intermittent — it doesn't happen every launch — which is a strong signal of a timing/race condition rather than a logic error.

Full exception:
```
System.Reflection.TargetInvocationException
  Inner: NullReferenceException: Object reference not set to an instance of an object.
   at TaleWorlds.GauntletUI.BaseTypes.TextWidget..ctor(UIContext context)
   at TaleWorlds.GauntletUI.PrefabSystem.WidgetFactory.CreateBuiltinWidget(...)
   ...
   at TaleWorlds.MountAndBlade.GauntletUI.GauntletDefaultLoadingWindowManager.Initialize()
   at TaleWorlds.Engine.LoadingWindow.InitializeWith[T]()
   at TaleWorlds.MountAndBlade.GauntletUI.GauntletUISubModule.OnApplicationTick(Single dt)
```

### Root Cause: A Null Captured at Construction Time

The crash chain has three steps:

**Step 1 — `GauntletLayer` is constructed too early**

`GauntletLayer..ctor` calls `InitializeContext()`, which creates a `UIContext` by passing `UIResourceManager.FontFactory` as a constructor argument. On v1.3.15, `GauntletLayer` is first constructed during `GauntletUISubModule.OnApplicationTick` (very first tick), which can fire before the `UIResourceManager` has finished its initial `Refresh()` — specifically before `FontFactory` has been assigned. At that point `UIResourceManager.FontFactory` is `null`.

**Step 2 — The null is captured into `UIContext`**

`UIContext` stores the `FontFactory` reference it receives as a field. Because the null was passed at construction time (`_initializedWithExistingResources = true`), the `UIContext.Initialize()` call skips re-creating `FontFactory`. The instance permanently holds a null `FontFactory` — even after `UIResourceManager.FontFactory` is later populated by a subsequent `Refresh()`.

**Step 3 — `TextWidget` dereferences it**

`GauntletDefaultLoadingWindowManager.Initialize()` loads a UI movie onto that same `GauntletLayer`. Loading a movie constructs widget objects including `TextWidget`, whose constructor calls `context.FontFactory.DefaultFont`. `context.FontFactory` is the null captured in Step 2 → `NullReferenceException`.

The intermittency is explained by the race: if the resource initialisation completes before the first `GauntletLayer` is constructed (the common case), everything works. A slower machine, different module load order, or background I/O activity can shift the timing enough to hit the null.

### Why an `InitializeContext` Prefix Doesn't Work

The first attempted fix — a Harmony prefix on the private `InitializeContext()` method — compiled and appeared correct but reliably produced no log output and no effect. The root cause is the HarmonyLib behaviour with prefix exceptions: if the prefix itself throws (e.g. because `UIResourceManager.Refresh()` throws during early startup before resources are configured), HarmonyLib silently swallows the exception and allows the original method to run unchanged. The build also hit two separate compile errors (`CS0718` — static class as generic type argument for the logger; `CS0012` — `UIResourceManager.SpriteData` referencing `TaleWorlds.TwoDimension.dll` which isn't in the project references) that required multiple iterations to fix, eroding confidence in that approach.

The fix was redesigned to target the `GauntletLayer` **constructor** instead via a postfix, where exceptions are visible and `__instance` is available for a proper context rebuild.

---

## The Fix

### `source/Coop/BootPatches.cs` *(new file)*

Contains a Harmony **postfix** on `GauntletLayer(string, int, bool)` — the constructor — registered via `AccessTools.Constructor` rather than a string-based method lookup.

**Why a constructor postfix instead of a prefix on `InitializeContext`:**
- A postfix fires *after* the constructor (and therefore after the internal `InitializeContext()` call), giving us `__instance` so we can inspect `UIContext.FontFactory` on the actual object that was built.
- `AccessTools.Constructor` is Harmony's safe API for constructor lookups — more reliable than `GetMethod` by string on a private method.
- The postfix is outside the silent-swallow zone: any exception it throws is surfaced normally rather than swallowed by HarmonyLib.

**What the postfix does:**

1. Checks `__instance.UIContext?.FontFactory` — if non-null, the race didn't happen and it exits immediately (no-op on every normal call).
2. If null: calls `UIResourceManager.Refresh()` to force a full synchronous resource initialisation — this sets `ResourceDepot`, `SpriteData`, `FontFactory`, `BrushFactory`, and `WidgetFactory`.
3. Calls `GauntletLayer.OnResourceRefreshEnd(emptyList)` via reflection. This is the engine's own "resources changed" recovery path: it calls `InitializeContext()` again, this time with `UIResourceManager.FontFactory` non-null, replacing the broken `UIContext` with a valid one.
4. The empty list argument is safe because no movies are loaded at the time the constructor runs.

**Diagnostics:**

`FileLog()` writes to `BootPatches.log` via `System.IO.File.AppendAllText` entirely independently of Serilog. This was added after the first iteration failed silently — `FileLog` bypasses the Serilog pipeline and confirms whether `Apply()` ran, whether the ctor was found, and whether the postfix fired, even if the logger isn't configured yet.

**Fallback path:**

If `AccessTools.Constructor` returns null (e.g. the constructor signature changes in a future game version), `Apply()` falls back to patching `InitializeContext()` as a prefix on the private method. This path is a best-effort safety net; the primary path is the ctor postfix.

**Logger:**

`private static ILogger Logger => Serilog.Log.ForContext(typeof(BootPatches))` — a property, not a field. Static classes cannot be used as generic type arguments (`CS0718`), so `GetLogger<BootPatches>()` cannot be used here. The property form also ensures the logger is resolved after Serilog is configured rather than at class initialisation time.

A dedicated `Harmony` instance (`"Coop.BootFix"`) is used to keep boot-time patches isolated and easy to identify in Harmony diagnostic output.

### `source/Coop/Coop.csproj`

Two additions:

1. **`TaleWorlds.GauntletUI` reference** — `BootPatches.cs` references `UIResourceManager` and `GauntletLayer`, which live in `TaleWorlds.Engine.GauntletUI.dll`. Without an explicit reference the project fails to compile.

2. **`<Compile Include="BootPatches.cs" />`** — `Coop.csproj` is a legacy-format project (not SDK-style) and does not auto-discover `.cs` files. Without this entry the compiler silently omits the file — no error, no patch, no fix.

### `source/Coop/CoopMod.cs`

`BootPatches.Apply()` is called at the top of `NoHarmonyLoad()`, before `CoopartiveMultiplayerExperience` is constructed and before any `Updateables` are registered. `NoHarmonyLoad()` runs from `OnSubModuleLoad()`, which fires before any application ticks — ensuring the Harmony patch is active before `GauntletLayer` is first constructed.

Serilog is initialised in `NoHarmonyInit()`, which runs immediately before `NoHarmonyLoad()`, so the logger in `BootPatches` is available at call time.

---

## Why Each Part Is Necessary

| Change | Without It |
|---|---|
| `BootPatches.cs` — ctor postfix + `Refresh()` + `OnResourceRefreshEnd()` | The race condition is never addressed. `UIContext` gets constructed with null `FontFactory` and the null is permanently captured. |
| `BootPatches.cs` — `FileLog()` | Silent failures (Serilog not yet configured, Harmony swallowed exception) were invisible. `FileLog` writes to disk regardless and was essential to confirming the patch was actually running. |
| `Coop.csproj` — `TaleWorlds.GauntletUI` reference | Project fails to compile — `UIResourceManager` and `GauntletLayer` are undefined. |
| `Coop.csproj` — `<Compile>` entry | `BootPatches.cs` is silently excluded from compilation. |
| `CoopMod.cs` — `BootPatches.Apply()` call | The patch class exists but is never registered with Harmony. Nothing fires. |

All five changes are required together — any one missing and the fix does not take effect.

---

## What Was Tried First (and Why It Failed)

**Attempt 1 — Prefix on `GauntletLayer.InitializeContext()`:**
- Looked up `InitializeContext` by string via `GetMethod(..., NonPublic | Instance)` and patched it as a prefix that called `UIResourceManager.Refresh()` if `FontFactory == null`.
- Build errors: `CS0718` (logger type argument) and `CS0012` (`SpriteData` from unreferenced assembly) required multiple iterations.
- After fixing those, the patch produced zero log output. Investigation confirmed the prefixes on private methods can fail silently inside HarmonyLib when the prefix throws — and `Refresh()` throws during very early startup before `ResourceDepot` is configured. HarmonyLib swallows the exception and runs the original unchanged.

**Attempt 2 — Prefix on `InitializeContext` with targeted `RefreshFontFactory()` first:**
- Added reflection call to private `RefreshFontFactory()` as the primary path, with `Refresh()` as fallback, both wrapped in try-catch.
- Still no log output. The problem was that the static field initializer pattern (`private static readonly MethodInfo _refreshFontFactory = typeof(...).GetMethod(...)` plus `private static readonly Harmony _harmony = new Harmony(...)`) caused static constructor issues — any exception during class initialization faults the type permanently and subsequent access rethrows `TypeInitializationException`. With Serilog logging calls in `Apply()` and no fallback to disk, these failures were invisible.

**Attempt 3 — Final (this PR):**
- Moved all Harmony construction and reflection inside `Apply()` (no static field initializers).
- Added `FileLog()` for disk-based diagnostics independent of Serilog.
- Switched from `InitializeContext` prefix to `GauntletLayer` ctor postfix via `AccessTools.Constructor`.
- Added `OnResourceRefreshEnd` call to properly rebuild the `UIContext` rather than just setting the static `UIResourceManager` state.
- Confirmed working: five consecutive game launches without crash.
