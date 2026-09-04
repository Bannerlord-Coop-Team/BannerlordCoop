# Dedicated-server synthetic verification

The `dedicated-server-synthetic` profile is a bounded check between BannerlordCoop and the real
standalone dedicated-server process. It exercises UDP transport, password and module validation,
fresh-controller validation, disconnect/reconnect generations, and cleanup without starting
rendered clients.

It is not a campaign-join or save-apply test. The controller fails closed unless the server exposes
authoritative serving state, its resolved join port, module contract, loaded artifact identities,
and the current connection roster.

## Safe protocol boundary

The permitted wire surface is the versioned `bannerlord-coop.ds-synthetic-wire.v1` manifest in
`DedicatedServerWireManifest`. Its tests pin the exact Common protobuf type IDs, channel/delivery
selection, directions, wrapper shape, and manifest digest. Keep that executable manifest and its
production round-trip tests as the source of truth; do not duplicate its numeric IDs here.

The scenario uses this boundary:

1. Prove the machine-readable wrong-password rejection.
2. Prove an intentional co-op build mismatch is denied before controller validation.
3. Read the server's authoritative provider-order module contract.
4. Connect two peers, validate that exact contract, then send distinct fresh controller IDs.
5. Observe the campaign-time heartbeat and fresh-controller response.
6. Disconnect and reconnect one peer, proving a new connection identity and no stale roster entry.
7. Disconnect both peers and prove final cleanup.

The result is marked `protocolShortcut=true`: it proves only the pre-character validation response.
Synthetic peers must never send `NetworkTransferNewHero`, `NetworkPlayerCampaignEntered`,
`NetworkJoinSync`, or `NetworkPlayerMissionEntered`. Those messages enter real campaign, save, or
mission state and require rendered clients.

The optional save collector assembles only bounded compressed bytes. It accepts at most 4,096
chunks, 64 KiB per chunk, 64 MiB compressed, and 256 MiB declared uncompressed. It never
decompresses or loads a campaign, and save receipt is not required for this profile.

## Commands

The offline node is a protocol lab, not dedicated-server evidence:

```text
VerificationHarness dedicated-server-synthetic-node \
  --role server \
  --scenario baseline \
  --port 4201 \
  --timeout-ms 5000 \
  --run-token ds-synthetic-run \
  --request-id ds-synthetic-request \
  --password-env BANNERLORD_COOP_TEST_PASSWORD \
  --module-contract <base64-json-contract> \
  --expected-clients 2
```

Module-mismatch and wrong-password servers use `--expected-clients 1`; wrong-password also omits
`--module-contract`. Client nodes use `--role client`, one of the two fixed `--controller-id`
values, and no `--expected-clients`. Baseline and module-mismatch clients require the module
contract; wrong-password clients omit it.

The real-process controller attaches to the opt-in live-test pipe for an already running exact
server PID. It does not launch Bannerlord or the server:

```text
VerificationHarness dedicated-server-synthetic \
  --head <exact-coop-commit> \
  --tree <exact-coop-tree> \
  --server-head <exact-server-commit> \
  --server-tree <exact-server-tree> \
  --server-pid <pid> \
  --run-token <token> \
  --request-id <id> \
  --join-port <port> \
  --password-env BANNERLORD_COOP_TEST_PASSWORD \
  --artifact-manifest <manifest.json> \
  --artifact-manifest-sha256 <64-hex> \
  --artifact-root <staged-root> \
  [--timeout-ms 30000] \
  [--seed 0] \
  [--output evidence.json]
```

Passwords are read only from the named environment variable. They are never written to arguments,
evidence, errors, hashes, or replay identity.

## Artifact and status contract

The source-bound build creates `dedicated-server-synthetic-artifacts.v1`; the runtime controller
only verifies it. The caller supplies the frozen raw manifest hash separately. The manifest binds
both repositories' head/tree pairs, the co-op build version, stage-relative paths, hashes, assembly
versions, and MVIDs. It contains the six managed co-op assemblies, `DedicatedServer.Core`, the active
Windows or Linux shim, and the platform's TaleWorlds starter assembly.

Before UDP work and again after the lifecycle, the controller verifies:

- the raw and canonical manifest digests plus both requested source identities;
- status PID, role, run token, and process start time against the OS process;
- every loaded assembly's allowlisted path, SHA-256, MVID, and version;
- unchanged manifest and process identities at postflight.

The server's opt-in `status` result must supply:

- `buildVersion`, `processStartedUtc`, and `serving`;
- the resolved `joinPort`;
- the complete provider-order `moduleValidation` contract;
- `loadedAssemblies` and `dedicatedServerAssemblies` with name, version, MVID, and location;
- `connectionRoster` entries with controller ID, unique connection-instance ID, connected state,
  and join state.

`serving` and `joinPort` come from the host's authoritative state. Module order and every production
field stay stable across all snapshots. Loaded-assembly status observes assemblies already bound in
the serving AppDomain; it never loads one to satisfy attestation. Roster state comes from current
connections, not registered players, and disconnected entries are removed. Console parsing and
`registeredPlayers` are not accepted substitutes.

## Evidence

`dedicated-server-synthetic.evidence.v1` records both exact source identities, required checks,
topology, manifest identity, lifecycle snapshots, artifact hashes, timestamps, and verdict. Evidence
copies source identities only from the verified staged manifest.

Replay identity covers source, profile, seed, options, controller IDs, manifest hash, and module
contract. The logical state digest excludes timestamps, PIDs, paths, ports, tokens, passwords,
campaign ticks, and recyclable connection IDs. A passing controller result is one input to the
source-bound orchestration receipt; it cannot authorize caller-supplied source labels by itself.
