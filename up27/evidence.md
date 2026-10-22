# Lady's Knight Out

## What the issue is

Lady's crime penalty used a private Honor store that conflicted with the shared per-player trait service. A late join during an influence solution could also inherit the owner's completion flag and journal.

## What the evidence shows

The real village action now gives A the crime failure and canonical server Honor-50, while B remains unchanged. A real co-op tournament loss and a pending-solution join also finish with the correct owner journal and unchanged B rewards.

The server and two clients ran e5d2345f99e7. Reward values come from retained game-state reads; the journal images show the actual outcomes. These checks do not exercise a trait-level threshold or preservation of the owner's completed history across reload.

## Village crime

![A has Sora's active tournament quest before the village crime.](VillageCrime-before-A.png)

![B has no active quest before A forces the village's goods.](VillageCrime-before-B.png)

![A's completed journal records Sora rejecting the champion after the crime.](VillageCrime-journal-A.png)

![B's journal remains without A's new crime completion or active quest.](VillageCrime-journal-B.png)

The shared server store ends at Honor-50 for A. B's store remains absent, with the same gold, renown, influence and relation.

## Tournament failure

![A enters the real Vostrum co-op tournament before losing the first match.](TournamentFailure-before-A.png)

![B stays on the campaign map while A enters the tournament.](TournamentFailure-before-B.png)

![A's completed journal confirms failure to reach Sora's required second round.](TournamentFailure-journal-A.png)

![B's journal remains unchanged after A's tournament failure.](TournamentFailure-journal-B.png)

The server applies one relation penalty and no quest rewards. A's canonical Honor stays-50; B remains unchanged.

## Joining during the influence solution

![A keeps Nus's acceptance conversation open until B finishes joining.](PendingJoin-before-completion-A.png)

![B reaches the campaign map while A's influence solution remains pending.](PendingJoin-before-completion-B.png)

![A's completed Nus journal contains the acceptance and influence-solution outcome.](PendingJoin-journal-A.png)

![B's native journal has no completed Nus entry after A finishes.](PendingJoin-journal-B.png)

B loaded the pending issue with its attempted flag false. A then paid18 influence and received7 renown and5 relation; B's values did not change. Older Lady entries remain visible, and retained giver-specific archive reads establish the absence of a completed Nus entry on B.

All owned processes were confirmed absent and the bench released before this report was prepared.
