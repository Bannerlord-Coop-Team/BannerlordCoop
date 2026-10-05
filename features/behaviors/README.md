# Individual behavior leaves

The [action catalog](../catalog.csv) supplies navigation groups. Each leaf below has its own
precondition and outcome in [behavior-leaves.csv](../behavior-leaves.csv).
[Source records](../baseline/behavior-sources.json) bind installed type, DLL hash and source span.
Read [the mapping contract](../granularity.md) before using a leaf as an acceptance oracle.

[Structural source paths](../source-paths.md) are a separate navigation inventory and are excluded
from behavior-leaf counts. Only interpreted cases have established source outcomes; definition
and candidate leaves retain their own unresolved work. No aggregate inherits a leaf's pass.

| Area | Leaves | Interpreted | Definitions | Candidate |
| --- | ---: | ---: | ---: | ---: |
| [ai](ai.md) | 31 | 0 | 0 | 31 |
| [alleys](alleys.md) | 18 | 0 | 0 | 18 |
| [armies](armies.md) | 29 | 0 | 0 | 29 |
| [battles](battles.md) | 105 | 0 | 0 | 105 |
| [buildings](buildings.md) | 22 | 0 | 0 | 22 |
| [caravans](caravans.md) | 18 | 0 | 0 | 18 |
| [character](character.md) | 29 | 0 | 0 | 29 |
| [clans](clans.md) | 47 | 12 | 0 | 35 |
| [crafting](crafting.md) | 35 | 0 | 0 | 35 |
| [heroes](heroes.md) | 52 | 0 | 0 | 52 |
| [inventory](inventory.md) | 40 | 0 | 0 | 40 |
| [kingdoms](kingdoms.md) | 68 | 5 | 0 | 63 |
| [locations](locations.md) | 44 | 0 | 0 | 44 |
| [movement](movement.md) | 132 | 0 | 89 | 43 |
| [naval](naval.md) | 39 | 0 | 12 | 27 |
| [options](options.md) | 25 | 0 | 0 | 25 |
| [party](party.md) | 113 | 49 | 1 | 63 |
| [presentation](presentation.md) | 54 | 0 | 0 | 54 |
| [prisoners](prisoners.md) | 33 | 0 | 0 | 33 |
| [progression](progression.md) | 1154 | 6 | 1095 | 53 |
| [quests](quests.md) | 68 | 31 | 0 | 37 |
| [save](save.md) | 31 | 0 | 0 | 31 |
| [sessions](sessions.md) | 31 | 0 | 0 | 31 |
| [sieges](sieges.md) | 52 | 0 | 0 | 52 |
| [tools](tools.md) | 30 | 0 | 0 | 30 |
| [tournaments](tournaments.md) | 37 | 0 | 0 | 37 |
| [towns](towns.md) | 92 | 0 | 35 | 57 |
| [trade](trade.md) | 43 | 7 | 1 | 35 |
| [vanilla-modes](vanilla-modes.md) | 17 | 0 | 0 | 17 |
| [villages](villages.md) | 66 | 0 | 27 | 39 |
| [workshops](workshops.md) | 17 | 0 | 0 | 17 |

Totals by knowledge: candidate=1202, definition-inspected=1260, source-interpreted=110
Runtime states: unrun=2572
