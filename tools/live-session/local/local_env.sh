#!/usr/bin/env bash
# Installed scripts default to their own orchestration directory; worktree tests can override it.
here=${COOP_LIVE_ORCHESTRATION_ROOT:-$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)}
codex_root=${COOP_LIVE_CODEX_ROOT:-$(dirname "$here")/.codex}
runtime_root=${COOP_LIVE_RUNTIME_ROOT:-$(cygpath -u "$USERPROFILE")/.codex-runtime/live-testing}
skill=$codex_root/skills/issue-to-pr/scripts
integration=$codex_root/skills/start-integration-test/scripts/start-integration-test.sh
rotate=$codex_root/skills/rotate/scripts/rotate.sh
hidden=$codex_root/runtime/hidden-windows-exec/hidden_windows_exec.sh
powershell=$(cygpath -u "$WINDIR")/System32/WindowsPowerShell/v1.0/powershell.exe
live_root=$runtime_root/BannerlordCoop
dedicated_root=$runtime_root/BannerlordCoop.DedicatedServer
inputs=$codex_root/runtime/issue-to-pr/local-dedicated-server
