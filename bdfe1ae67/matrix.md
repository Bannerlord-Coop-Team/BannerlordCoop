# Artisan product sales live matrix

Head `bdfe1ae6716a61c0ade1ea42743e814a59c64dc2`. All 28 rows are banked: 16 on the exact head and 12 retained under Andrew's debug-command waiver. No original-brief runtime row remains open.

| Case | Tested head | Evidence |
|---|---|---|
| busy-owner-return | `bdfe1ae67` | [R46 lifecycle](r46-lifecycle.md) |
| character-replacement | `bdfe1ae67` | [R46 lifecycle](r46-lifecycle.md) |
| companion-failure | `bdfe1ae67` | [Companion ending capture](companion-failure-owner.png) |
| companion-success | `bdfe1ae67` | [Companion ending captures](companion-success-owner.png) |
| concurrent-direct-ownership | `bdfe1ae67` | [R42 direct matrix](r42-direct.md) |
| death-giver | `75f402bd2` | [Andrew waiver](scope.md) |
| death-merchant | `75f402bd2` | [Andrew waiver](scope.md) |
| death-target | `75f402bd2` | [Andrew waiver](scope.md) |
| direct-delivery-refusal | `bdfe1ae67` | [R42 direct matrix](r42-direct.md) |
| direct-full-delivery-B | `bdfe1ae67` | [R42 direct matrix](r42-direct.md) |
| direct-merchant-refusal | `bdfe1ae67` | [R42 direct matrix](r42-direct.md) |
| direct-merchant-surrender | `bdfe1ae67` | [R42 direct matrix](r42-direct.md) |
| direct-no-goods | `bdfe1ae67` | [R42 direct matrix](r42-direct.md) |
| direct-partial-and-remainder-A | `bdfe1ae67` | [R42 direct matrix](r42-direct.md) |
| direct-ruler-merchant-surrender | `bdfe1ae67` | [R42 direct matrix](r42-direct.md) |
| direct-timeout | `bdfe1ae67` | [R42 direct matrix](r42-direct.md) |
| inheritance | `bdfe1ae67` | LOCAL R27 |
| lord-eligibility | `75f402bd2` | [Andrew waiver](scope.md) |
| lord-success | `75f402bd2` | [Andrew waiver](scope.md) |
| overpriced-betrayal | `bdfe1ae67` | LOCAL R27 |
| overpriced-eligibility | `75f402bd2` | [Andrew waiver](scope.md) |
| overpriced-success | `75f402bd2` | [Andrew waiver](scope.md) |
| save-restart-direct | `75f402bd2` | [Andrew waiver](scope.md) |
| stale-betrayal | `75f402bd2` | [Andrew waiver](scope.md) |
| stale-success | `75f402bd2` | [Andrew waiver](scope.md) |
| temporary-reconnect | `bdfe1ae67` | [R43 native rejoin](r43-reconnect.md) |
| war-external | `75f402bd2` | [Andrew waiver](scope.md) |
| war-player | `75f402bd2` | [Andrew waiver](scope.md) |

The [scope decisions](scope.md) retain the explicit Overpriced supplemental-delta exclusion and designed save-restart outcome. Historical rows are not relabeled as fresh runs.

The full exact-head HIVE job docker-ci-f0cd9c33e95c4329bb79 passed the product build and eight E2E shards: 7,689 passed, zero failed, three declared skips. Framework audit: GenericFolder=0, removed-framework refs=0, ancestor9e85bfe3c present. AGENTS lint: FAIL=0 WARN=1 for guarded client replay. Exact-head Codex incremental CLEAN is retained.

The PR remains draft pending Andrew's tech-debt choice and fresh Muse review after 2026-11-09 00:10Z.
