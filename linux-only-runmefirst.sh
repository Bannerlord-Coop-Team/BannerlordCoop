#!/bin/bash

# -e exit immediately if error, -u unset variable is error, -o pipefail fail on any pipeline command
set -euo pipefail
PATTERN='MountAndBlade' # pattern works for me™

echo "Start the Bannerlord launcher now."
read -r -p "Press Enter once it's running... "

# There might be some strangeness here still. I will list out what each option does for clarity since I sure don't keep them in memory:
# pgrep: -f match against full command line. -i ignore case.
# grep -v invert match -x match whole line -e patterns. In the case below ( 2 patterns ) it will search and match for both.
# in the case of grep it filters out the script processes (was having weirdness with this).
pid="$(pgrep -f -i "$PATTERN" | grep -vx -e "$$" -e "$PPID" | head -n1 || true)"
if [[ -z "$pid" ]]; then
    echo "No matching process found. Make sure the launcher is running, then try again."
    exit 1
fi

game_dir="$(readlink -f "/proc/${pid}/cwd/../.." 2>/dev/null || true)"
echo "Detected game path: ${game_dir}"
read -r -p "Is this correct? [y/N] " answer

if [[ "${answer,,}" != "y" && "${answer,,}" != "yes" ]]; then
    echo "Aborting. No symlink created."
    exit 0
fi

# its easiest to just delete folder and generate new symlink
rm -rf "./mb2"
ln -sfn "$game_dir" "./mb2"
echo "Linked ./mb2 -> ${game_dir}"
echo "If this did not work correctly just do it manually."
