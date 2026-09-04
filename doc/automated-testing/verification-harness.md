# Verification harness

`VerificationHarness` maps an exact source identity and changed-path set to the cheapest cumulative
verification profile that can observe the change. Repository code owns this decision; skills and
host scripts consume its schema-versioned JSON plan instead of maintaining another rule table.

| Profile | Runtime | What it proves |
| --- | --- | --- |
| `unit` | ordinary test process | pure logic, codecs, and state machines |
| `deterministic-peer` | isolated peer graphs in one process | wire copies, authority, ordering, registry use, and game-thread application |
| `process-peer` | one server and two client processes | real UDP, Common serialization, reconnect, and process cleanup |
| `dedicated-server-synthetic` | standalone server and two lightweight peers | boot, password, pre-save join protocol, connection generations, and server cleanup |
| `rendered-smoke` | standalone server and two rendered clients | native client boot and a narrow functional or visual witness |
| `full-live` | complete live campaign | UI, mission, Steam, save/load, and cross-client acceptance |

Profiles are cumulative and blocking. Selecting `process-peer`, for example, also requires `unit`
and `deterministic-peer`. Unknown paths, invalid paths, and unavailable required executors fail
closed; they never lower the selected tier. Native, UI, scene, mission, input, Steam, save/load,
runtime-patching, and otherwise unclassified production boundaries remain `full-live`.

## Plan and validate

Generate a plan with repository-relative paths as arguments:

```text
dotnet run --project source/VerificationHarness/VerificationHarness.csproj -- plan --head <40-hex-commit> --tree <40-hex-tree> source/Common/Network/MessagePacket.cs
```

For orchestration, prefer newline-delimited stdin:

```text
git diff --name-only origin/development...HEAD | dotnet run --project source/VerificationHarness/VerificationHarness.csproj -- plan --head <40-hex-commit> --tree <40-hex-tree> --stdin
```

Pull-request CI runs the complete local handoff:

```text
bash .github/scripts/run-verification-plan.sh <base-commit> artifacts/verification-plan
```

The script requires a clean checkout, derives `HEAD`, `HEAD^{tree}`, and the rename-expanded
merge-base diff itself, clears known outputs, and rebuilds the Common and harness roots from empty
`bin`/`obj` directories. It independently validates the generated plan against the authoritative
changed paths:

```text
dotnet run --project source/VerificationHarness/VerificationHarness.csproj --no-build -- validate-plan --plan <plan.json> --head <40-hex> --tree <40-hex> --base <40-hex> --changed-paths <newline-list-path> --output <receipt.json>
```

The `verification-plan-receipt.v1` output binds the authoritative base and the SHA-256 of the
canonical changed-path array. Its scope is `selection-and-local-harness-handoff` and
`includesTestEvidence` is false. It records external runtime requirements as
`blocked-external-runtime`; it does not relabel those requirements as passed or waived.

The complete plan/result contract is
[`verification-report-v1.schema.json`](verification-report-v1.schema.json). Tests keep its
serialized property sets, tier catalog, check catalog, and schema version aligned with the planner.

## Process-peer transport lab

Create a manifest from the already-built exact source snapshot, then run the stable suite:

```text
dotnet run --project source/VerificationHarness/VerificationHarness.csproj --no-build -- process-peer-manifest --head <40-hex> --tree <40-hex> --output <manifest.json>
dotnet run --project source/VerificationHarness/VerificationHarness.csproj --no-build -- process-peer-suite --head <40-hex> --tree <40-hex> --seed <0x16-hex> --artifact-manifest <manifest.json>
```

The suite starts one LiteNetLib UDP loopback server and two process-isolated clients. Typed lab
payloads cross Common's protobuf serializer and type mapper. It covers convergence, reconnect,
malformed and out-of-sequence frames, corrupt acknowledgements, intentional divergence, deadlines,
and orphan cleanup. Negative controls pass only when the expected failure is observed.

The v2 manifest pins every harness `.dll`, `.deps.json`, and `.runtimeconfig.json`, the host
executable, `System.Private.CoreLib`, framework and runtime versions, OS, and process architecture.
The controller and every child must reproduce that identity. The manifest is source provenance only
when its producer derived the head/tree from the immutable snapshot it built.

This tier does not execute Coop message handlers, campaign state, Bannerlord native code, rendering,
or save loading. Those claims require a higher profile.

## Peer-host protocol

`peer-host --instance-id <id>` is the JSON-lines process-isolation foundation used by the transport
lab. Requests and responses contain `protocolVersion`, a sequence starting at 1, `instanceId`, and
`processId`. The first command is `hello`; later commands are `ping`, `put`, `get`, `snapshot`, and
`shutdown`.

Snapshots hash canonical sorted logical state. Process IDs and timestamps stay outside that digest.
Blank, oversized, malformed, unsupported, or out-of-sequence input emits one error at the expected
sequence and exits with code 4. EOF and an acknowledged shutdown exit cleanly.

## Evidence and rollout

Plans bind the exact commit/tree, schema version, required profiles/checks, topology, seed,
replay identity, and stable evidence fields. Process IDs and timestamps remain explanatory metadata,
not convergence inputs. Peer-bearing profiles always require one server and two clients.

Rendered or full-live evidence remains required for native, visual, Steam, real save/load,
process-bootstrap, or user-visible claims. A screenshot-capable client is still a rendered client;
synthetic peers cannot replace framebuffer evidence. During rollout, backtest historical defects,
retain seeded negative controls, and shadow narrower classifications against live outcomes before
lowering a path family. Any escaped defect, unmapped path, simulator drift, or incomplete evidence
returns that family to `full-live`.
