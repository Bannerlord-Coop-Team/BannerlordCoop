# Quests and issues in co-op

Vanilla issues and quests assume one player. This folder is the shared code that lets several players
use them in one campaign. Only quest types that have been wired up here run. Every other vanilla issue
stays switched off.

How it works and how to add a quest type are in [AGENTS.md](AGENTS.md).

Quests only run in nightly builds and local Debug builds. Stable releases have every issue and quest
switched off, and a server running one cancels any quest a nightly save still has in progress.

## What players get

- Every player sees the same issue on the same notable or lord, built from the server's values.
- The first player to accept an issue owns it. Everyone else sees it as taken and nothing more: no quest,
  no journal entry, no pop-up.
- Rewards, relations and endings are worked out by the server as the owning player. Clients only show the
  result.
- Quests are not saved between sessions. A quest ends when its player leaves or the server restarts.

## Trying it in game

Use a server and a client. All commands are `coop.debug.issues.*`.

- `list_types` (both sides) lists the registered quest types.
- `give <hero_id> <quest_type>` (server) gives a notable or a lord an issue. Player heroes are refused.
- `complete <hero_id> [success|fail|betrayal|cancel|timeout|ailord]` (server) ends it. With an accepted quest it
  runs the outcome as the owner. Without one only `timeout`, `cancel` and `ailord` work, `ailord` has the
  settlement owner's clan leader solve it the way an AI lord would. It calls
  `CompleteQuestWithSuccess()` and the other endings directly, so it skips the quest's branch methods, where
  vanilla pays rewards, moves items and changes settlements. Use it to check that an ending is announced and
  replayed, not that a success pays out.
- `army_needs_supplies.setup <controller_id>` (server) gets a player ready to take Army Needs Supplies from
  Rhagaea, and `army_needs_supplies.stock` (server) tops up the owner's party with what the quest asks for.

Example on Danustica (`town_ES1`): stand in the town on a client and run
`coop.debug.settlements.list_notables`. It prints each notable's id and what they offer. On the server run
`coop.debug.issues.give <id of the gang leader> GangLeaderNeedsToOffloadStolenGoodsIssue`, accept it
from the client, then end it with `coop.debug.issues.complete <same id> cancel`.

To check who sees what, use two clients: the second client should see no quest, no pop-up and no
"updated" marker for the first client's quest, including after it leaves and rejoins.
