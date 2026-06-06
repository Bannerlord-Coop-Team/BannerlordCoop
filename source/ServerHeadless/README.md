# ServerHeadless

A console host that loads a Bannerlord campaign save **fully headless** — no native engine, no
graphics, no window. It boots just enough of the managed game on its own, lets you pick a save from
a console menu, loads it into a live `Campaign`, and (next milestone) ticks the simulation.

```
[ServerHeadless] Loaded in 9 ticks.
[ServerHeadless] Campaign loaded:
    Heroes:      1725 alive
    Parties:     1167
    Settlements: 493
    Main hero:   Acarion
[ServerHeadless] Idle. Press CTRL+C to stop.
```

## Why not just run the engine?

`Bannerlord.exe` is a native host (`WotsMainSDLL`) that boots the engine, calls the engine-internal
`Module.Initialize()`, and owns the per-frame tick. A plain managed process can't reach that path —
`Module.CreateModule()` immediately NREs on native engine globals (`Utilities.SetLoadingScreenPercentage`).
So instead of driving the engine, `ServerHeadless` stands up the **managed** game on its own and
stubs the genuinely-native calls.

## How it works

1. **Bootstrap without the engine** ([`HeadlessBootstrap`](Bootstrap/HeadlessBootstrap.cs)):
   - Construct `Module` via its private constructor (reflection) and assign `Module.CurrentModule`,
     skipping `CreateModule()`'s native side effects.
   - `MBObjectManager.Init` + register the core campaign types; `BannerManager.Initialize`.
   - Install a managed [`IPlatformFileHelper`](Bootstrap/HeadlessPlatformFileHelper.cs) (the engine
     normally provides this) mapping `PlatformFileType.User` → `Documents\Mount and Blade II Bannerlord`,
     so the save system can enumerate/read `.sav` files. Set the file save driver.
   - `ModuleHelper.InitializeModules(Native, SandBoxCore, SandBox, StoryMode, Coop)` and, per module,
     `XmlResource.GetXmlListAndApply(...)` to register each module's `<Xmls>` so the **real** game
     data (cultures, items, skills, strings, settlements, …) can be loaded.
2. **Console save menu** — `MBSaveLoad.GetSaveFiles()`, newest first.
3. **Load the save** — `MBSaveLoad.LoadSaveGameData(name)` → `MBGameManager.StartNewGame(new SandBoxGameManager(loadResult))`,
   then tick `GameStateManager.Current.OnTick(dt)` until the game manager reports loaded. On success
   `Campaign.Current` is populated from the save.
4. **CTRL+C** requests a graceful shutdown.

The decisive design choice is **letting the real managed data pipeline run** (`MBObjectManager.LoadXML`,
`Game.SetBasicModels`, `Campaign.OnInitialize`, and a postfix that drives `SandBoxManager.InitializeSandboxXMLs`
in place of the SandBox submodule we don't load) and only Harmony-stubbing the engine-native surface
(map scene, pathfinding, weather, siege frames, edit-mode, the scene-backed map-distance model). The
Harmony patches live under [`Bootstrap/Patches/`](Bootstrap/Patches); each is small and documents the
native dependency it replaces.

## Running

Run it from — or point it at — the game's `bin\Win64_Shipping_Client` directory (it needs the game
assemblies and a deployed `Modules` folder for the active modules' data):

```
ServerHeadless.exe "E:\Path\To\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client"
```

With no argument it falls back to the current directory, then searches upward for the in-repo
`mb2\bin\Win64_Shipping_Client` junction created by `prepare-links.ps1`.

## Build notes

- Targets **net472 / x64** to match the game runtime and host the net472 TaleWorlds assemblies in
  process. Game assemblies are resolved at runtime from the install directory via an
  `AssemblyResolve` handler.
- TaleWorlds references resolve through the repo's `mb2` junction (`..\..\mb2\...`), created by
  `prepare-links.ps1`. In a git worktree, create the junction in the worktree root too.
- Build with **Visual Studio's MSBuild** — the project uses `Krafs.Publicizer`, and the publicized
  reference assemblies only expose private members when every dependency assembly is referenced.

## Status

`Campaign` load is complete and verified (full data, e.g. 493 settlements on the `MP` save). Next
milestone: tick `Campaign` without crashing. A few patches added before the data-loading approach
landed may now be redundant and can be pruned once confirmed unreached.
