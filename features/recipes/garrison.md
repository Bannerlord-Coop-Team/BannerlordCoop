# Danustica garrison component lifecycle

Status: draft, source-inspected only; live exercise blocked as recorded in
[authoring evidence](../evidence/authoring.md).
Source: [TownDebugCommand](../../source/GameInterface/Services/Towns/Commands/TownDebugCommand.cs),
particularly `GarrisonBacklinkCoopCommand`, `ApplyGarrisonLifecycleCoopCommand` and
`FormatGarrisonBacklink`, plus [FiefSync](../../source/GameInterface/Services/Fiefs/FiefSync.cs).
This checks a component lifecycle, not the full garrison UI.

Follow [runtime](runtime.md) with an isolated disposable campaign and two clients. Confirm
the commands in `list_commands`; use the read-only `coop.debug.town.list_towns`, `[]`, to
confirm Danustica's town object `town_comp_ES1`. The settlement ID `town_ES1` is different.

Doctor: server/client1/client2 each execute `coop.debug.town.garrison_backlink`,
`["town_comp_ES1"]`. Require exactly one active garrison, a registered backlink component,
the same active party ID and `backlinkMatchesActive=True`. Retain this baseline and town info.
If the campaign has no garrison, duplicates or an unregistered component, record the actual
precondition failure. Do not create or discard unrelated parties to satisfy this recipe.

Drive: only the server executes `coop.debug.town.apply_garrison_lifecycle`,
`["town_comp_ES1","finalize"]`. This calls `GarrisonPartyComponent.OnFinalize()` on that one
active component. Read the backlink command on every peer: expect `backlinkComponent=null`,
`backlinkParty=null`, `activeGarrisonCount=1`, the same original active party ID, and
`backlinkMatchesActive=False`. Installed v1.4.8 `GarrisonPartyComponent.OnFinalize` clears the
town backlink without destroying the party. `FiefSync` registers that field and both lifecycle
methods as write targets; the real test must observe the resulting replication.

Then only the server executes the same command with `["town_comp_ES1","initialize"]` if
the precondition still reports exactly one active garrison and no conflicting component.
It calls `OnInitialize()` and refuses a different existing backlink. Read both clients and
server again: the expected original active party and registered backlink must match, with
`backlinkMatchesActive=True`. Retain before/action/after responses and logs.

The installed source oracle was inspected in full: `OnInitialize` assigns this component to the
town backlink, and `OnFinalize` clears it. The owning CampaignSystem hash is in
[provenance](../provenance.md); the methods occupy decompiled type lines 107-110 and 112-115.
Reinspect if the installed hash differs. The expected values above are established independently
of the debug command's returned output; their live replication remains unverified.

Safe negative slice: server invokes `["town_comp_ES1","invalid_operation"]` while the baseline
preconditions hold. Require `command_failed`, an unknown-action message and unchanged backlinks.
This does not replace the positive real lifecycle witness.

Cleanup: after a known successful finalize, restore with the initialize operation only while
its original component is still valid and no conflicting authoritative change occurred. After
an uncertain response, read state/logs before any restoration attempt. This command does not
capture an automatic rollback fixture; isolation is required. Stop the owned run with confirmed
cleanup; preserve all evidence. If restoration fails, retain it as a failure and do not save
the mutated test campaign as an accepted baseline.

Excluded: garrison creation/destruction, recruiting, troop transfers, upgrade XP, wages,
starvation, siege defenses, governor effects and live release acceptance.
