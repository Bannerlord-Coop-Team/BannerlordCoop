#!/bin/bash

# -e exit immediately if error, -u unset variable is error, -o pipefail fail on any pipeline command
set -euo pipefail
PATTERN='MountAndBlade' # pattern works for me™
MANAGED_COMMENT='# set by BannerlordCoop linux-only-runmefirst.sh'
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Prints the install root of the running Bannerlord process, or nothing when it isn't running.
detect_game_dir() {
    # pgrep: -f match against full command line. -i ignore case.
    # grep -v invert match -x match whole line -e patterns, filters out this script's own processes.
    local pid
    pid="$(pgrep -f -i "$PATTERN" | grep -vx -e "$$" -e "$PPID" | head -n1 || true)"
    if [[ -z "$pid" ]]; then
        return 0
    fi
    readlink -f "/proc/${pid}/cwd/../.." 2>/dev/null || true
}

# Wraps a value in single quotes so both sh and bash read it back literally.
shell_quote() {
    local escaped="${1//\'/\'\\\'\'}"
    printf "'%s'" "$escaped"
}

# Replaces any BANNERLORD_DIR export in the given profile with the managed line, keeping everything else.
write_profile_export() {
    local profile="$1"
    local line="$2"
    local kept=""
    if [[ -f "$profile" ]]; then
        # grep exits 1 when every line was filtered out; any other failure must not truncate the profile.
        kept="$(grep -v -e '^[[:space:]]*export BANNERLORD_DIR=' "$profile")" || [[ $? -eq 1 ]]
    fi
    {
        if [[ -n "$kept" ]]; then
            printf '%s\n' "$kept"
        fi
        printf '%s\n' "$line"
    } > "$profile"
    echo "Set BANNERLORD_DIR in ${profile}"
}

# Points out a leftover ./mb2 from the old link-based setup without touching it.
report_old_link() {
    local old="${repo_root}/mb2"
    if [[ -L "$old" ]]; then
        echo "Note: ${old} is an old link that is no longer used. Remove just the link with: unlink \"${old}\""
        return 0
    fi
    if [[ -e "$old" ]]; then
        echo "Note: ${old} is no longer used. It is not a link, so check what it holds before removing it."
    fi
}

game_dir="${1:-}"
if [[ -z "$game_dir" ]]; then
    echo "Start the Bannerlord launcher now."
    read -r -p "Press Enter once it's running... "
    game_dir="$(detect_game_dir)"
    if [[ -z "$game_dir" ]]; then
        echo "No matching process found. Make sure the launcher is running, then try again."
        echo "Or pass the install folder directly: $0 /path/to/Bannerlord"
        exit 1
    fi
fi

game_dir="$(readlink -f "$game_dir" || printf '%s' "$game_dir")"
echo "Detected game path: ${game_dir}"
read -r -p "Is this correct? [y/N] " answer

if [[ "${answer,,}" != "y" && "${answer,,}" != "yes" ]]; then
    echo "Aborting. BANNERLORD_DIR was not set."
    exit 0
fi

if [[ ! -d "${game_dir}/bin/Win64_Shipping_Client" ]]; then
    echo "${game_dir} has no bin/Win64_Shipping_Client folder, so it isn't a Bannerlord install. Nothing was changed."
    exit 1
fi

export_line="export BANNERLORD_DIR=$(shell_quote "$game_dir") ${MANAGED_COMMENT}"
# ~/.profile covers login and desktop sessions (IDEs), ~/.bashrc covers interactive terminals.
write_profile_export "${HOME}/.profile" "$export_line"
if [[ -f "${HOME}/.bashrc" ]]; then
    write_profile_export "${HOME}/.bashrc" "$export_line"
fi

report_old_link
echo "BANNERLORD_DIR=${game_dir}"
echo "Open a new terminal (or log out and back in for IDEs), or run this in the current shell:"
echo "  export BANNERLORD_DIR=$(shell_quote "$game_dir")"
echo "Other shells (zsh, fish) need the same variable set in their own startup file."
