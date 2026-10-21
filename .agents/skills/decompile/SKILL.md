---
name: decompile
description: Decompile Bannerlord game types from the DLLs under the mb2 junction before asserting anything about vanilla behavior. Use when you need to know what vanilla game code actually does (method bodies, field writers, which side sets state) for BannerlordCoop work.
---

# decompile

Wraps `ilspycmd` so the game assemblies don't get rediscovered every session. Run it from anywhere inside a checkout; it walks up from cwd to the `mb2` junction, and from a worktree it falls back to the main checkout's junction.

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

- `mb2/bin/Win64_Shipping_Client/TaleWorlds.*.dll` and `SandBox*.dll` (core engine/campaign types)
- `mb2/Modules/*/bin/Win64_Shipping_Client/*.dll` (SandBox/Alley/GauntletUI module types live here, not the top-level bin)

Type lists are cached in `~/.cache/bannerlord-decompile/` and invalidated when a DLL's mtime changes (game update).
