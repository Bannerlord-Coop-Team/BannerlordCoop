# Automated testing architecture

## Outcome

Bannerlord live testing should become the last verification tier, not the default test runner for
every change. The repository selects the cheapest tier that can observe the changed behavior, runs
all cheaper tiers first, and escalates whenever classification or evidence is incomplete.

The design has six cumulative profiles:

| Profile | Runtime | Primary proof | Target use |
| --- | --- | --- | --- |
| `unit` | ordinary test process | pure logic, codecs, state machines | every change |
| `deterministic-peer` | one process, isolated peer object graphs | wire copies, authority, ordering, registry and game-thread application | managed synchronization |
| `process-peer` | one synthetic server process and two synthetic client processes | real UDP, Common serialization, poll loops, reconnect and cleanup | transport-lab and harness lifecycle changes |
| `dedicated-server-synthetic` | real standalone server plus two lightweight clients | boot, password, protocol compatibility, connection generations and server cleanup | dedicated-server and join boundaries |
| `rendered-smoke` | real standalone server plus two rendered clients | native client boot and a narrow functional or visual witness | native/render boundary |
| `full-live` | complete existing live campaign | player-visible, mission, UI, Steam, save/load and cross-client acceptance | irreducible behavior |

These are tiers, not alternatives. Selecting `process-peer` requires `unit`, `deterministic-peer`,
and `process-peer` to pass on the same source identity.

## Why this is faster

The current live lane spends most of its time on engine startup, save preparation, process rotation,
campaign loading, focus-safe window management, and screenshot collection. Those costs are paid even
when the defect is a protobuf field, stale sequence, registry lookup, or poller/game-thread ordering
bug.

The new design moves those assertions into deterministic and process-isolated tests that can run in
parallel on ordinary CI. A rendered process is reserved for claims that depend on Bannerlord's
native engine or framebuffer. The expected steady-state shape is many cheap protocol/state peers and
one rendered witness, with the existing two-client rendered topology retained wherever cross-client
or visual evidence is required.

Initial performance targets are budgets to measure, not current benchmark claims:

- `unit`: under 5 minutes;
- `deterministic-peer`: under 10 minutes;
- `process-peer`: under 5 minutes after build artifacts are warm;
- `dedicated-server-synthetic`: under 10 minutes with an immutable server/save fixture;
- `rendered-smoke`: under 15 minutes;
- `full-live`: unchanged until shadow evidence proves a narrower profile is safe.

## Fail-closed planning

`VerificationHarness plan` is the repository-owned classification boundary. It accepts exact Git
head and synthetic-tree identities plus repository-relative changed paths, and emits a versioned JSON
plan. Skills and host scripts consume that result instead of maintaining a second rule table.

Rules use the maximum required tier, never a weighted score. High-risk input cannot be diluted by
low-risk files. Empty, rooted, traversing, malformed, or otherwise invalid input blocks. A valid but
unrecognized path selects `full-live`. Missing executors or artifacts also block; they never fall back
to a lower tier. Production Common networking and packet-handler paths require at least
`dedicated-server-synthetic`; the synthetic process lab alone is not accepted as product-handler
evidence.

Path rules are intentionally conservative during rollout. Symbol and reverse-dependency analysis can
lower a family only after its simulator capability and historical defect coverage have been proven.
Native, UI, scene, mission, input, Steam, reflection-only, and unknown boundaries remain full-live.

## Exact evidence contract

Every plan and result is bound to:

- the exact tested commit and synthetic tree;
- schema and classifier versions;
- required profiles and stable check ids;
- topology, deterministic seed, and replay identity;
- start/completion timestamps;
- state digests with explainable fields;
- artifact paths and SHA-256 hashes;
- process instance ids, process ids, and exit codes where applicable;
- `functional` or `visual` evidence profile for rendered smoke.

A hash is not a useful oracle by itself. Peer snapshots retain canonical sorted fields so a mismatch
can identify the divergent owner, registry id, sequence, lifecycle state, or queue counter. Process
ids and timestamps stay outside convergence digests.

Rendered live may be waived only when all required evidence exists for the exact tree and no native,
visual, Steam, real save-load, process-bootstrap, or user-visible claim remains. During shadow mode,
the existing live decision stays authoritative even when the planner predicts a cheaper tier.

## Deterministic peer simulator

The ordinary E2E router now crosses the real serialization boundary per recipient. Logical messages
use `MessagePacket.Create`, the production reliable-message batcher, bare or aggregate wire payloads,
and the production packet handlers through a small test receive replica. A broadcast no longer shares
the sender's object or one
deserialized instance across clients. Tests can opt into the real receive shape: publish on a
poller-marked thread, then explicitly pump that recipient's own FIFO game queue. Each simulated
process has a distinct queue context, so pumping client A cannot execute client B's callback under
client A's container.

The virtual network models:

- directed links with FIFO only for `ReliableOrdered` within one channel;
- unordered delivery that may overtake, plus sequenced supersession without a false global order;
- independent reliable-ordered, reliable-unordered, unreliable, sequenced, world, and bulk domains;
- virtual latency and legal cross-domain reordering;
- directional pause and resume;
- connection generations, disconnect cancellation, and reconnect;
- bounded pending traffic, high-water marks, and explicit backpressure;
- deterministic traces, replay inputs, and connection transitions;
- callback failure without silently discarding later ready work.

Required negative controls include a missing protobuf field, two messages published before one game
drain, stale traffic across reconnect, intentional state divergence, and a registry lookup performed
on the wrong thread. Each seeded defect must make its selected check fail.

This tier cannot prove process-global isolation, real sockets, native mission behavior, rendering, or
Bannerlord save loading.

## Synthetic process transport lab

The `process-peer` tier runs one synthetic server and two synthetic client processes over real
LiteNetLib loopback using the Common serializer/type mapper. It verifies distinct process identities,
protocol version, monotonic sequences, wire hashes, synthetic-state convergence,
disconnect/reconnect generations, bounded timeouts, explicit shutdown acknowledgements, and orphan
cleanup. A content-addressed artifact manifest must match every loaded runtime `.dll`, `.deps.json`,
and `.runtimeconfig.json` beneath the harness runtime directory before any child starts. Every child
also reports the same artifact-set digest. The manifest and replay identity additionally bind the
exact shared .NET framework patch, OS and process architecture, host executable hash, and core-library
hash. Pull-request CI pins SDK `10.0.300` instead of accepting a floating `10.0.x` runtime.

The JSON-lines `peer-host` protocol remains a smaller lifecycle seam for controller and digest tests.
The real transport controller is the required `process-peer` executor. A malformed request,
out-of-sequence request, process exit, missing response, digest mismatch, or cleanup failure produces
a nonzero result and machine-readable failure evidence.

This tier does not send production Coop messages, run `CoopNetworkBase`, load Campaign, install
Harmony patches, execute product handlers, or cross the native engine boundary. It is intentionally
not a passing oracle for production Common networking paths; those paths escalate to the blocked
dedicated-server tier.

## Dedicated-server synthetic tier

The synthetic tier targets a real standalone dedicated-server process. Its default safe scope is:

- boot/readiness and command-catalog discovery;
- correct and incorrect connection-password outcomes;
- multiple accepted LiteNetLib peers;
- campaign-time heartbeat observation;
- compatible and incompatible module/build negotiation;
- fresh-controller resolution without claiming character/save completion;
- disconnect, reconnect, connection-generation, roster, and shutdown cleanup;
- bounded connection and memory load with N lightweight clients.

It must not fabricate `NetworkTransferNewHero` or claim that a synthetic peer applied a campaign
save. Existing-player validation enters the real save-transfer path and is fixture-only, explicitly
bounded, and disconnected immediately after the intended receipt assertion. Full join-tail and
campaign convergence still require a real client until a faithful save consumer exists.

A stable implementation needs first-class pipe status for connection roster/state in the dedicated-
server repository. Parsing human console output is acceptable for a diagnostic spike, not as the
long-term blocking oracle.

## Rendered smoke and full live

A screenshot-capable client is still a rendered client. A stripped dedicated server or logic-only
peer cannot provide equivalent framebuffer evidence.

Rendered smoke keeps one real standalone server and two clients, but runs a narrow scenario:

1. prove exact deployed assemblies and process identity;
2. make one authoritative server mutation;
3. observe the result read-only on both clients;
4. collect only the screenshots needed for the asserted boundary;
5. prove crash, focus, and cleanup behavior.

Screenshot evidence records request and completion frames/times, BMP dimensions and bit depth,
stable file length, SHA-256, and basic pixel sanity. Empty, malformed, all-black, all-white, and
near-uniform frames are rejected. The capture state machine requires matching fresh observations on
different engine frames and is unit-tested independently of Bannerlord. Pixel sanity does not claim
semantic visual correctness.

`Utilities.ToggleRender()` is an experimental render-on-demand optimization. Managed inspection only
proves that it crosses the native `IUtil.ToggleRender` boundary. A live canary must prove that game
frames and networking continue while rendering is disabled, rendering can be safely restored, a
fresh non-uniform screenshot follows, and GPU work actually drops. Until then, status reports label
the tracked state as an assumption and never claim native confirmation.

If hidden/minimized rendering stalls presentation or produces stale frames, use normal clients on
isolated virtual displays with GPU-backed Windows workers. Do not modify the native renderer to make
a no-render process synthesize screenshots.

## Fixture, cache, and worker model

- Build once per exact synthetic tree and publish a content-addressed DLL/deploy manifest.
- Keep immutable known-good save fixtures and verify hashes before every reuse.
- Reuse a warm campaign only within one source identity and scenario family.
- Give rendered workers separate Windows users, Documents/config/save/temp/log roots, displays, and
  run tokens.
- Use a virtual-display GPU worker for visual/native smoke and CPU workers for peer/synthetic scale.
- Keep nightly/release full-live, clean-install, upgrade, locale, Steam, input, and soak matrices even
  when an individual pull request qualifies for a cheaper tier.

## Safety and orchestration

All Windows console executables run through the hidden Windows host. The planner and runner fail when
the live module does not match the intended immutable deploy manifest. A compile that unexpectedly
deploys is recorded as runtime contamination and blocks every runtime profile until an explicit
rotate or restore establishes the intended source.

Queue admission is waived for this testing-stack development program only. That waiver does not
waive exact-head review, CI-equivalent verification, the required two-client topology, runtime
evidence, or cleanup. Direct coordination replaces durable queue allocation for this program.

The shared issue-to-PR skill, broker, bridge, and runtime remain owned by the skills-audit cutover.
Repository work exposes stable CLI and JSON contracts; shared orchestration consumes them after the
cutover so interactive and background tasks do not execute divergent skill copies.

Pull-request CI runs `.github/scripts/run-verification-plan.sh` after the ordinary unit and E2E jobs.
Every job checks out the durable `pull_request.head.sha`, which is also the exact feature head used by
issue-to-PR after development has been merged into the branch. The script derives that head/tree and
a rename-expanded authoritative PR merge-base diff from Git. It refuses tracked changes, untracked
files, and ignored files in the harness/Common compile roots, except generated `bin`/`obj` output. It
also checks repository and `source`-level MSBuild, package, SDK, and NuGet configuration inputs. It
removes known prior output files before building, so the recorded tree identifies the compiled source
and an aborted rerun cannot expose stale passing evidence. Common and harness `bin`/`obj` directories
are rebuilt from empty state, with incremental compilation disabled. It regenerates
the plan, and independently validates the plan against that full path list. The receipt binds the
authoritative base and canonical changed-path digest, then the job runs repository-owned profiles and
uploads the plan, receipt, artifact manifest, and process evidence. The receipt is only a selection
and local-harness handoff (`includesTestEvidence: false`); ordinary unit/E2E results and process or
runtime evidence remain separate. Required Windows runtime profiles stay `blocked-external-runtime`
until the issue-to-PR orchestrator supplies separate exact-tree evidence, and that handoff status does
not fail the local job or get rewritten as passed.

## Rollout

1. Run the planner in shadow mode while existing live selection remains authoritative.
2. Backtest historical defects and seed equivalent negative controls.
3. Compare identical scenarios across deterministic E2E, real process loopback, and rendered live.
4. Shadow at least 30 to 50 qualifying pull requests with zero unique-runtime misses, zero unmapped
   paths, deterministic reruns, and exact evidence binding.
5. Canary pure Common and proven managed campaign families first; keep full live out of band.
6. Reduce sampling only after each rule family has caught real or injected defects.
7. Freeze a family back to `full-live` after any escaped defect, simulator drift, new shim, missing
   evidence, or classifier error.

The end state is not "no live testing." It is fast managed and process evidence for most synchronization
work, a short rendered witness for native boundaries, and the complete live campaign only where it
adds information the cheaper tiers cannot observe.
