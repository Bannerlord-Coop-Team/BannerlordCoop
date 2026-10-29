#!/usr/bin/env bash
# Decompiles Bannerlord game types from the DLLs in the BANNERLORD_DIR game install.
#   decompile.sh <Full.Type.Name> [out.cs]   decompile a type to a scratch file
#   decompile.sh -l <pattern>                find a type name and its owning DLL
set -euo pipefail

die() { echo "error: $*" >&2; exit 1; }

# Finds ilspycmd on PATH or in the default dotnet global tools folder.
find_ilspy() {
    local c
    for c in ilspycmd ilspycmd.exe "$HOME/.dotnet/tools/ilspycmd" "$HOME/.dotnet/tools/ilspycmd.exe"; do
        if command -v "$c" >/dev/null 2>&1; then
            command -v "$c"
            return 0
        fi
    done
    return 1
}

ILSPY=$(find_ilspy) || die "ilspycmd not found; install it with: dotnet tool install -g ilspycmd"

# Converts a path for ilspycmd, which is a Windows exe under WSL and needs a Windows path there.
tool_path() {
    if command -v wslpath >/dev/null 2>&1; then wslpath -w "$1"; else echo "$1"; fi
}

# Converts a Windows-style path (C:\... or C:/...) to this shell's form under Git Bash or WSL.
unix_path() {
    case "$1" in
        [A-Za-z]:[\\/]*) ;;
        *) echo "$1"; return 0 ;;
    esac
    if command -v cygpath >/dev/null 2>&1; then cygpath -u "$1"; return 0; fi
    if command -v wslpath >/dev/null 2>&1; then wslpath -u "$1"; return 0; fi
    echo "$1"
}

# Resolves the game's top-level bin folder from BANNERLORD_DIR, the Bannerlord install root.
find_game_bin() {
    [ -n "${BANNERLORD_DIR:-}" ] || die "BANNERLORD_DIR is not set; run runmefirst.cmd (Windows) or linux-only-runmefirst.sh, then restart the shell"
    local root
    root=$(unix_path "$BANNERLORD_DIR")
    root="${root%/}"
    [ -d "$root/bin/Win64_Shipping_Client" ] || die "BANNERLORD_DIR=$BANNERLORD_DIR has no bin/Win64_Shipping_Client; point it at the Bannerlord install root"
    echo "$root/bin/Win64_Shipping_Client"
}

BIN=$(find_game_bin)

# Core/campaign assemblies in the top-level bin; module assemblies (SandBox, Alley, GauntletUI)
# live under Modules/*/bin/Win64_Shipping_Client instead.
dlls=()
while IFS= read -r f; do dlls+=("$f"); done < <(ls "$BIN"/TaleWorlds.*.dll "$BIN"/SandBox*.dll 2>/dev/null)
while IFS= read -r f; do dlls+=("$f"); done < <(ls "$BIN"/../../Modules/*/bin/Win64_Shipping_Client/*.dll 2>/dev/null)
[ ${#dlls[@]} -gt 0 ] || die "no DLLs found in $BIN"

CACHE_DIR="$HOME/.cache/bannerlord-decompile"
mkdir -p "$CACHE_DIR"

# Cached `ilspycmd -l` type list per DLL; invalidated by the DLL's mtime (game updates).
types_for() {
    local dll="$1"
    local base mtime cache
    base=$(basename "$dll" .dll)
    mtime=$(stat -c %Y "$dll" 2>/dev/null || stat -f %m "$dll")
    cache="$CACHE_DIR/$base.types"
    if [ ! -s "$cache" ] || [ "$(head -1 "$cache" 2>/dev/null)" != "# mtime=$mtime" ]; then
        { echo "# mtime=$mtime"; "$ILSPY" -l cise "$(tool_path "$dll")" 2>/dev/null; } > "$cache.tmp" || true
        mv "$cache.tmp" "$cache"
    fi
    echo "$cache"
}

list_mode=0
if [ "${1:-}" = "-l" ]; then
    list_mode=1
    shift
fi
[ $# -ge 1 ] || die "usage: decompile.sh [-l <pattern> | <Full.Type.Name> [out.cs]]"

if [ "$list_mode" = 1 ]; then
    pattern="$1"
    found=0
    for dll in "${dlls[@]}"; do
        cache=$(types_for "$dll")
        # ilspycmd lists one FQN per line; nested types appear as Outer/Inner
        if grep -i -- "$pattern" "$cache" | grep -v '^#'; then
            echo "    <- $(basename "$dll")"
            found=1
        fi
    done
    [ "$found" = 1 ] || die "no type matching '$pattern'"
    exit 0
fi

fqn="$1"
out="${2:-}"
# Normalize nested-type separator: cache lists use '/'
fqn_norm="${fqn//+//}"

owner=""
for dll in "${dlls[@]}"; do
    cache=$(types_for "$dll")
    if grep -Fq -- "$fqn_norm" "$cache"; then
        owner="$dll"
        break
    fi
done
if [ -z "$owner" ]; then
    echo "error: exact type '$fqn' not found; try: decompile.sh -l $(basename "${fqn##*.}")" >&2
    exit 1
fi

short="${fqn##*.}"
short="${short//\//_}"
[ -n "$out" ] || out="/tmp/$short.cs"

"$ILSPY" "$(tool_path "$owner")" -t "$fqn" > "$out" || die "ilspycmd failed for $fqn in $(basename "$owner")"
echo "$out ($(wc -l < "$out") lines, from $(basename "$owner"))"
