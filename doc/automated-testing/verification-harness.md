# Verification harness

`VerificationHarness` is the repository-owned boundary between a changed-path set and the tests
that must run. It emits a schema-versioned JSON plan so orchestration does not need to reproduce
classification rules in a skill or host script.

The six profiles are cumulative and blocking: `unit`, `deterministic-peer`, `process-peer`,
`dedicated-server-synthetic`, `rendered-smoke`, and `full-live`. An unrecognized path selects
`full-live`. Empty, rooted, traversing, or otherwise malformed input also selects every profile,
sets `decision` to `blocked-invalid-input`, and exits with code 3.

Run the planner with paths as arguments:

```text
dotnet run --project source/VerificationHarness/VerificationHarness.csproj -- plan --head 1111111111111111111111111111111111111111 --tree 2222222222222222222222222222222222222222 source/Common/Network/MessagePacket.cs
```

For orchestration, newline-delimited stdin avoids shell quoting:

```text
git diff --name-only origin/development...HEAD | dotnet run --project source/VerificationHarness/VerificationHarness.csproj -- plan --head 1111111111111111111111111111111111111111 --tree 2222222222222222222222222222222222222222 --stdin
```

The `profiles` array always contains all six profiles in ordinal order. `required` identifies the
cumulative subset that must pass. Every profile has `blocking: true`; an executor that cannot run a
required action must stop instead of falling back to a lower profile.

Pull-request CI consumes the plan with:

```text
bash .github/scripts/run-verification-plan.sh <base-commit> artifacts/verification-plan
```

The script requires a clean checkout before it derives `HEAD` and `HEAD^{tree}`. It rejects tracked
changes, ordinary untracked files, and ignored files beneath the harness, harness tests, or Common
compile roots except generated `bin`/`obj` output. The same ignored-file gate covers repository and
`source`-level `Directory.Build`, `Directory.Packages`, `global.json`, and NuGet configuration inputs.
It clears its known plan and process-evidence outputs before building, so a failed rerun cannot leave
a stale passing artifact at the requested output path. It also removes `bin` and `obj` for Common,
the harness, and harness tests, then performs a non-incremental build. Reused local worktrees therefore
cannot relabel an older ignored binary as output from the current tree.

The consumer derives a rename-expanded changed-path list directly from the authoritative PR
merge-base diff (`base...head`). Validation independently receives that list and recomputes the complete classifier output,
so a plan that omits a higher-tier path fails even when every remaining field is internally
consistent. The `verification-plan-receipt.v1` output records the 40-hex authoritative base and a
SHA-256 digest of the canonical normalized changed-path array. It lists `harnessOwnedProfiles` and
keeps dedicated/rendered/full runtime requirements explicitly `blocked-external-runtime`.

The receipt's fixed scope is `selection-and-local-harness-handoff`, with
`includesTestEvidence: false`. It proves plan selection and names work for this job or an external
executor; it does not report that unit, E2E, process, or Windows runtime checks passed. Those results
remain in their own jobs and evidence artifacts. An external runtime requirement therefore does not
fail this local job by itself, and the blocked status is not a pass or waiver.

The validator CLI accepts the independently produced Git input explicitly:

```text
dotnet run --project source/VerificationHarness/VerificationHarness.csproj --no-build -- validate-plan --plan <plan.json> --head <40-hex> --tree <40-hex> --base <40-hex> --changed-paths <newline-list-path> --output <receipt.json>
```

For `process-peer`, the `repository-dotnet-run` executor expands the first argument as the project
path and invokes `dotnet run --project <project> -- process-peer-suite <remaining arguments>`. It replaces
`{source.head}`, `{source.syntheticTree}`, and `{seed}` with the fields from the same immutable plan;
unresolved or mismatched values are a blocking orchestration error.

`--head` is the exact tested commit and `--tree` is the exact synthetic tree containing the tested
working state. Both are required 40-hex Git object ids. The plan carries them in `source`, derives a
stable seed and replay identity from the tree, and hashes the canonical plan material. Its stable
check ids are `unit`, `wire-copy-e2e`, `poller-game-thread`, `deterministic-peer`, `process-peer`,
`dedicated-server-synthetic`, `rendered-smoke`, and `full-live`. Runtime fields are never omitted:
pending checks have a `pending` verdict, null timestamps and state digest, and empty artifact-hash
and process-exit arrays. Peer-bearing topologies always require one server and two clients. The
rendered-smoke check selects `evidenceProfile: visual` for UI/rendering paths and `functional`
otherwise.

## Synthetic process transport lab

First generate a manifest from the already-built exact source snapshot:

```text
dotnet run --project source/VerificationHarness/VerificationHarness.csproj --no-build -- process-peer-manifest --head <40-hex> --tree <40-hex> --output <manifest.json>
```

Then run the stable command:

```text
dotnet run --project source/VerificationHarness/VerificationHarness.csproj --no-build -- process-peer-suite --head <40-hex> --tree <40-hex> --seed <0x16-hex> --artifact-manifest <manifest.json>
```

Each scenario starts one synthetic LiteNetLib UDP loopback server and two process-isolated synthetic
clients, and all typed lab payloads cross `Common`'s protobuf serializer/type-mapper boundary. The
blocking suite
runs convergence, reconnect, malformed-frame rejection, out-of-sequence rejection, corrupt-
acknowledgement rejection, intentional divergence, and deadline/orphan cleanup. Negative controls
pass only when the expected failure is observed. Its `process-peer-suite.evidence.v1` output records
every nested `process-peer.evidence.v1` result plus exact source identity, topology, required checks,
normalized state and replay digests, wire and payload hashes, process identities and exits,
artifact hashes, timestamps, and failures. This tier does not execute real Coop message handlers,
campaign state, Bannerlord native code, rendering, or save loading.

The v2 manifest pins every `.dll`, `.deps.json`, and `.runtimeconfig.json` under the harness runtime
directory and records one canonical artifact-set digest that every child must reproduce. It also
records the exact .NET framework description/version, runtime identifier, OS and process architecture,
host executable hash, and `System.Private.CoreLib` hash. Children report the same shared-runtime digest,
and the controller and every child must match the manifest's complete runtime identity, including the
host executable hash. Host/runtime identities participate in replay hashing. Its source
identity is authoritative only when the producer derives head/tree from the
immutable Git snapshot it built, as the CI script and issue-to-PR archive consumer do; a hand-authored
manifest is not source provenance.

The planner emits the canonical seed form `0x` plus 16 lowercase hexadecimal characters. The CLI
also accepts a non-negative decimal unsigned 64-bit value and normalizes it to that form; evidence
always records the canonical form.

## JSON-lines peer-host protocol v1

`peer-host --instance-id <id>` is a process-isolation foundation, not a LiteNetLib or Bannerlord
peer simulator. The caller supplies a deterministic instance id. Requests and responses are one
JSON object per line and include `protocolVersion`, `sequence`, `instanceId`, and `processId`.

Sequence numbers start at 1 and increase by one per accepted request. The first command must be
`hello`; the remaining commands are `ping`, `put`, `get`, `snapshot`, and `shutdown`. A snapshot
returns sorted raw fields plus a SHA-256 digest over canonical JSON. The fields contain the caller's
instance id, current sequence, lifecycle state, per-command counters, and process-local state.
Process ids and timestamps are intentionally outside the digest, so equivalent process instances
converge while the raw fields still explain a real divergence. EOF is a clean stop with
exit code 0. A blank, oversized, malformed, unsupported, out-of-sequence, or otherwise invalid
request emits one error response at the expected sequence and stops with exit code 4. This makes a
broken controller fail closed and leaves a versioned seam for real process-hosted peer adapters.
