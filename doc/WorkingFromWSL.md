# Working From WSL

The agent runs in WSL but the toolchain is Windows and is **not on the WSL PATH** (bare `dotnet`/`MSBuild` give "command not found"). Use the full paths, and don't reinstall, they are already there: dotnet at `/mnt/c/Program Files/dotnet/dotnet.exe`, MSBuild at `/mnt/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe`.

Git and builds over the `/mnt/c` mount are slow: a multi-commit `merge`/`fetch`/`push`, a codex review, or a Docker test run routinely blows past the 2-minute Bash default and gets SIGTERM'd (exit 143), and foreground Bash is hard-capped at 10 minutes. Run those with `run_in_background: true` (or a long `timeout`) from the start.

A freshly-created worktree has **no `mb2` junction** (only the main checkout does), so MSBuild/dotnet there fail with thousands of "game type not found" errors. Create a real Windows junction: `powershell.exe New-Item -ItemType Junction -Path <wt>\mb2 -Target '<game dir>'`; a WSL `ln -s` won't resolve for the Windows build, and a Docker run clobbers the junction so recreate it afterward.
