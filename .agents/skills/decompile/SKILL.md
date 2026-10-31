---
name: decompile
description: Decompile Bannerlord game types from the DLLs in the BANNERLORD_DIR game install before asserting anything about vanilla behavior. Use when you need to know what vanilla game code actually does (method bodies, field writers, which side sets state) for BannerlordCoop work.
---

# decompile

Wraps `ilspycmd` so the game assemblies don't get rediscovered every session. Run it from anywhere; it reads the game install from the `BANNERLORD_DIR` environment variable (set by `runmefirst.cmd`, forwarded to WSL through `WSLENV`) and fails with a clear error if that's unset or has no `bin/Win64_Shipping_Client`. A Windows-style value is converted with `cygpath -u` under Git Bash or `wslpath -u` under WSL.

## Usage

```bash
.agents/skills/decompile/scripts/decompile.sh <Full.Type.Name> [out.cs]   # decompile one type to a scratch file
.agents/skills/decompile/scripts/decompile.sh -l <pattern>                # find a type name and its owning DLL
```

- `<Full.Type.Name>` is the fully qualified name, e.g. `TaleWorlds.CampaignSystem.Encounters.PlayerEncounter`. Nested types use `Outer/Inner` or `Outer+Inner`.
- Output defaults to `/tmp/<short-name>.cs`. Decompiled types run 1000+ lines, so grep the file instead of pasting it whole.
- `-l` greps the cached type lists of all assemblies, case-insensitively, and prints `FQN <- dll`. Use it when you only know part of a name or the owning assembly.
- If `ilspycmd` is missing, install it with `dotnet tool install -g ilspycmd`. Under WSL the script converts paths with `wslpath -w` for the Windows exe.

## Where it looks

- `$BANNERLORD_DIR/bin/Win64_Shipping_Client/TaleWorlds.*.dll` and `SandBox*.dll` (core engine/campaign types)
- `$BANNERLORD_DIR/Modules/*/bin/Win64_Shipping_Client/*.dll` (SandBox/Alley/GauntletUI module types live here, not the top-level bin)

Type lists are cached in `~/.cache/bannerlord-decompile/` and invalidated when a DLL's mtime changes (game update).
