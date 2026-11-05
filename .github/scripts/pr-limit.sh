#!/usr/bin/env bash

# Posts the "PR limit" commit status on open PRs, so an author's ready PRs past the limit can't merge.
# The oldest ready PRs keep the slots, so merging, closing or drafting one frees a slot for the next.
# Usage: PR_REPO=owner/name [PR_AUTHOR=login] [PR_LIMIT=5] [DRY_RUN=1] bash .github/scripts/pr-limit.sh

set -euo pipefail

repo="$PR_REPO"
author="${PR_AUTHOR:-}"
limit="${PR_LIMIT:-5}"
context="PR limit"
failed=0

# Prints "author<TAB>isDraft<TAB>number<TAB>headSha" for every open PR, oldest first.
list_open_prs() {
    gh pr list --repo "$repo" --state open --limit 500 \
        --json number,author,isDraft,headRefOid,createdAt \
        --jq 'sort_by(.createdAt) | .[] | [.author.login, .isDraft, .number, .headRefOid] | @tsv' | tr -d '\r'
}

# Prints the state this check last posted on a commit, or nothing if it hasn't posted one.
current_state() {
    gh api "repos/$repo/commits/$1/statuses" --jq "[.[] | select(.context == \"$context\")][0].state // \"\"" | tr -d '\r'
}

# Posts a status unless the commit already has that state, because GitHub allows 1000 statuses per commit and context.
post_status() {
    local number="$1" sha="$2" state="$3" description="$4"

    if [[ -n "${DRY_RUN:-}" ]]; then
        echo "#$number $state: $description"
        return 0
    fi
    [[ "$(current_state "$sha")" == "$state" ]] && return 0

    gh api -X POST "repos/$repo/statuses/$sha" --silent \
        -f state="$state" -f context="$context" -f description="$description"
}

# Read into a variable first, because a process substitution would hide a failed listing and exit green.
open_prs="$(list_open_prs)"
[[ -z "$open_prs" ]] && exit 0
mapfile -t prs <<< "$open_prs"

# Ready PRs per author, so a blocked PR can say how far over the limit its author is.
declare -A ready_total ready_seen
for pr in "${prs[@]}"; do
    IFS=$'\t' read -r login is_draft _ _ <<< "$pr"
    [[ "$is_draft" == "true" ]] && continue
    ready_total[$login]=$(( ${ready_total[$login]:-0} + 1 ))
done

for pr in "${prs[@]}"; do
    IFS=$'\t' read -r login is_draft number sha <<< "$pr"
    state="success"
    description="Drafts don't count toward the limit of $limit ready PRs"

    if [[ "$is_draft" != "true" ]]; then
        ready_seen[$login]=$(( ${ready_seen[$login]:-0} + 1 ))
        description="Within the limit of $limit ready PRs per author"
        if (( ready_seen[$login] > limit )); then
            state="failure"
            description="$login has ${ready_total[$login]} ready PRs, the limit is $limit. Finish, close or draft an older one."
        fi
    fi

    [[ -n "$author" && "$login" != "$author" ]] && continue
    post_status "$number" "$sha" "$state" "$description" || {
        echo "::warning::Failed to post the $context status on #$number"
        failed=1
    }
done

exit "$failed"
