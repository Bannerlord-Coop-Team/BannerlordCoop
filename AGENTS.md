# mb2 — the game-install junction (READ BEFORE ANY DELETE OR WORKTREE CLEANUP)

Do NOT recursive delete the mb2 folder. This contains the entire game directory
and will delete 60 GB worth of files.

`mb2` (at the repo root AND at the root of most worktrees under
`.claude\worktrees\`) is a **Windows directory junction** pointing at the whole
Bannerlord install (`C:\Program Files (x86)\Steam\steamapps\common\Mount &
Blade II Bannerlord`). It is a build convenience for `..\mb2\` HintPaths — it
is NOT a normal folder, and it is NOT copied data.

Recursive deletes **follow the junction into the real install**: `rm -rf`
(Git Bash/MSYS), `Remove-Item -Recurse`, `rmdir /s`, and
`git worktree remove --force` / automatic worktree cleanup have all wiped the
game this way (2026-07-11 and 2026-07-18 — a 60 GB re-download each time).
Deletes bypass the Recycle Bin; there is no undo.

Rules:

1. **Never** run a recursive delete on `mb2` or on ANY directory tree that
   contains an `mb2` junction (a worktree, a temp clone, a moved folder).
2. **Before removing or cleaning up any worktree** — including automatic
   cleanup — check for `<worktree>\mb2` and unlink it FIRST, link-only:

       cmd /c rmdir "<worktree>\mb2"

   This removes just the junction, never the target's contents.
3. To make a worktree buildable again, recreate the junction:

       New-Item -ItemType Junction -Path "<worktree>\mb2" -Target "C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord"

4. Deploy/build tooling must never delete through `mb2` either — 
   `Deploy.targets` is deliberately copy-only (`MakeDir`/`Copy`); keep it that
   way and never reintroduce wipe-and-recreate deploy steps.
