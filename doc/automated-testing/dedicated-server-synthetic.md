# Dedicated-server synthetic verification

The `dedicated-server-synthetic` tier is a bounded functional check between
BannerlordCoop and the native dedicated-server host. It is intended to cover the
real process, real UDP transport, connection lifecycle, and the small pre-save
join protocol without launching rendered clients.

The real-process controller drives the opt-in dedicated-server build over UDP
and validates its token-scoped control pipe. It remains fail-closed when the
server does not expose the authoritative serving, join-port, and connection
roster fields described below.

## Safe protocol boundary

`bannerlord-coop.ds-synthetic-wire.v1` freezes the following Common protobuf
envelope IDs and LiteNetLib lanes:

| Payload | Type ID | Channel | Delivery | Direction |
| --- | ---: | ---: | --- | --- |
| `CampaignTimePacket` | `488231864` | 0 | Sequenced | server to client |
| `NetworkModuleVersionsValidate` | `1457133576` | 0 | ReliableOrdered | client to server |
| `NetworkModuleVersionsValidated` | `1206877260` | 0 | ReliableOrdered | server to client |
| `NetworkClientValidate` | `791628818` | 0 | ReliableOrdered | client to server |
| `NetworkClientValidated` | `29530214` | 0 | ReliableOrdered | server to client |
| `AggregateMessagePacket` | `1253361833` | 0 | ReliableOrdered | both |
| `GameSaveDataChunkPacket` | `404232623` | 1 | ReliableOrdered | server to client, optional |

The wrapper remains the production Common shape: protobuf field 1 is the stable
type ID and field 2 is the serialized payload. The harness uses independent
surrogate contracts rather than loading Coop.Core product types into the test
process. Golden IDs and the manifest hash therefore detect a real protocol
change instead of inheriting one silently from the product assembly.

`DedicatedServerProductionWireContractTests` separately round-trips the actual
product packet and message types through `ProtoBufSerializer` and a fresh
`SerializableTypeMapper`, including aggregate-message decoding and the real
channel/delivery selector. The harness source-shape regex test is only a
supplemental drift hint; it is not the production wire oracle.

The permitted scenario is deliberately short:

1. Connect with the configured LiteNetLib password and verify wrong-password
   rejection code `1` as a negative control.
2. Connect a separate negative-control peer, send an intentional co-op build
   mismatch, observe an explicit denial, and disconnect without sending
   `NetworkClientValidate`.
3. Read the authoritative provider-order module contract from dedicated-server
   status, then connect two distinct synthetic peers.
4. Each baseline peer sends that exact `NetworkModuleVersionsValidate` request,
   waits for `Matches=true` with the exact server build, and only then sends its
   fresh deterministic controller ID.
5. Observe a channel-0 Sequenced campaign-time heartbeat and
   `NetworkClientValidated(HeroExists=false, Player=null)`.
6. Mark that result `protocolShortcut=true`; it proves only the pre-character
   validation response.
7. Disconnect and reconnect through the real-process controller, then
   verify the authoritative connection roster has removed the old connection.

The synthetic peer must never send `NetworkTransferNewHero`,
`NetworkPlayerCampaignEntered`, `NetworkJoinSync`, or
`NetworkPlayerMissionEntered`. Those messages cross into real campaign object,
save-apply, or mission state and still require game clients. A fresh-controller
response is not evidence that a player entered the campaign.

## Bounded optional save collection

The optional save collector parses only fields 1-6 of
`GameSaveDataChunkPacket` and ignores later product metadata. It accepts at most
4,096 chunks, 64 KiB per chunk, 64 MiB compressed, and 256 MiB declared
uncompressed. Duplicate chunks, inconsistent transfer metadata, missing chunks,
and sizes beyond those limits fail before assembly. It never decompresses or
loads a campaign. Receipt of a save is not part of the required first version of
this tier.

## Offline process-node command

The offline node command uses LiteNetLib 1.3.1 with two channels, a 60-second
disconnect timeout, a 15 ms network update interval, and a 10 ms poll interval.
It is a protocol compatibility lab, not dedicated-server evidence.

```text
VerificationHarness dedicated-server-synthetic-node \
  --role server|client \
  --scenario baseline|module-mismatch|wrong-password \
  --port 4201 \
  --timeout-ms 5000 \
  --run-token ds-synthetic-run \
  --request-id ds-synthetic-request \
  --password-env BANNERLORD_COOP_TEST_PASSWORD \
  [--module-contract <base64-json-contract>] \
  [--controller-id ds-synthetic-client-a] \
  --expected-clients 2
```

`--expected-clients` belongs to the server node. The baseline server requires
exactly 2; each isolated negative-control server requires exactly 1. Baseline
and module-mismatch nodes require `--module-contract`; wrong-password nodes
reject it. Client nodes require one of the two fixed controller IDs and reject
`--expected-clients`.

The password is read only from the named environment variable. It is never put
in process arguments, node JSON, evidence JSON, error text, hashes, or replay
identity. The wrong-password probe derives a bounded non-secret wrong value and
does not print it.

## Real-process scenario command

The controller connects to the existing opt-in live-test pipe for the exact
dedicated-server PID, then creates the synthetic peers inside the harness
process. It does not start Bannerlord or the dedicated server.

```text
VerificationHarness dedicated-server-synthetic \
  --head <exact BannerlordCoop commit> \
  --tree <exact BannerlordCoop synthetic tree> \
  --server-head <exact dedicated-server commit> \
  --server-tree <exact dedicated-server synthetic tree> \
  --server-pid <pid> \
  --run-token <token> \
  --request-id <id> \
  --join-port 4201 \
  --password-env BANNERLORD_COOP_TEST_PASSWORD \
  --artifact-manifest C:\stage\dedicated-server-synthetic-artifacts.json \
  --artifact-manifest-sha256 0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef \
  --artifact-root C:\stage \
  [--timeout-ms 30000] \
  [--seed 0] \
  [--output evidence.json]
```

The named environment variable must contain the same non-empty password used by
the server. The value is never copied into arguments or evidence. The controller
first proves the machine-readable wrong-password rejection and the separate
module-mismatch denial. It then retains two peers that completed compatible
module validation before controller validation while it records
`before-connect`, `two-connected`,
`one-disconnected`, `reconnected`, and `final-empty` status snapshots. A
`registeredPlayers` count is explicitly rejected because it cannot prove which
network connections are current. The command exits zero only when the complete
lifecycle and every control identity check pass.

## Staged artifact receipt

`dedicated-server-synthetic-artifacts.v1` is created by the source-bound build
and staging step, not by the runtime controller. The orchestrator freezes the
SHA-256 of the exact manifest file and supplies it separately through
`--artifact-manifest-sha256`. Changing source labels or any manifest content
therefore invalidates that receipt.

The manifest records both repositories' head/tree pairs, the co-op build
version, stage-relative paths and SHA-256 values, and each managed assembly's
version and MVID. It must contain exactly the six co-op assemblies reported by
the status contract plus `DedicatedServer.Core`, the active
`DedicatedServer.Windows` or `DedicatedServer.Linux` shim, and
the platform's actual starter identity: `TaleWorlds.Starter.DotNetCore` on
Windows or `TaleWorlds.Starter.DotNetCore.Linux` on Linux. The serving child
process is commonly
`dotnet.exe`; its hash is runtime identity only and is never accepted as the
DedicatedServer source binding.

Before any UDP work and again after the lifecycle completes, the controller:

1. Verifies the frozen raw manifest hash, its canonical digests, and both
   requested source identities.
2. Matches the status PID, role, run token, and `processStartedUtc` to the OS
   process.
3. Resolves every status assembly location to its exact allowlisted path under
   `--artifact-root`.
4. Hashes the file, reads its PE MVID and assembly version, and compares all
   three values with both status and the manifest. `Common.dll` is verified
   before its build/module contract is trusted.
5. Requires the same manifest and process start identity in the postflight
   attestation.

Paths remain absent from evidence. A path outside the staged root, a duplicate
or missing simple assembly name, PID reuse, a changed artifact, or a relabeled
manifest fails the required `runtime-artifact-manifest-match` check.

## Dedicated-server status contract

The paired BannerlordCoop.DedicatedServer revision must expose these
authoritative fields from its opt-in live-test `status` result:

```json
{
  "buildVersion": "1.2.3+source",
  "processStartedUtc": "2026-09-01T12:00:00Z",
  "loadedAssemblies": [
    {
      "name": "Common",
      "version": "1.0.0.0",
      "mvid": "00000000-0000-0000-0000-000000000001",
      "location": "C:\\stage\\engine\\Modules\\Coop\\bin\\Common.dll"
    }
  ],
  "serving": true,
  "joinPort": 4201,
  "moduleValidation": {
    "coopBuildVersion": "1.2.3+source",
    "modules": [
      {
        "id": "Native",
        "isOfficial": true,
        "isDlc": false,
        "version": {
          "applicationVersionType": 4,
          "major": 1,
          "minor": 2,
          "revision": 3,
          "changeSet": 456
        }
      }
    ]
  },
  "dedicatedServerAssemblies": [
    {
      "name": "DedicatedServer.Core",
      "version": "1.0.0.0",
      "mvid": "00000000-0000-0000-0000-000000000000",
      "location": "C:\\staged-runtime\\DedicatedServer.Core.dll"
    }
  ],
  "connectionRoster": [
    {
      "controllerId": "ds-synthetic-client-a",
      "connectionInstanceId": "connection-a-1",
      "connected": true,
      "joinState": "ResolveCharacterState"
    },
    {
      "controllerId": "ds-synthetic-client-b",
      "connectionInstanceId": "connection-b-1",
      "connected": true,
      "joinState": "ResolveCharacterState"
    }
  ]
}
```

`serving` must come from the host's authoritative serving phase, and `joinPort`
must be the resolved port the co-op server actually bound. `moduleValidation`
must preserve the exact `IModuleInfoProvider` order and every production field;
the controller freezes it across every lifecycle snapshot. Any DLC entry,
missing official module, duplicate ID, build change, module change, or reorder
fails closed. `loadedAssemblies` is an observation of assemblies already bound
in the serving AppDomain; status never loads an assembly to make attestation
pass. `dedicatedServerAssemblies` separately exposes the loaded core,
platform shim, and TaleWorlds entry assemblies for staged artifact binding.
`connectionRoster`
must come from current connection objects, not `IPlayerManager.Players`; fresh
synthetic controllers have not registered heroes. Disconnected entries must be
removed. `connectionInstanceId` is an opaque, sanitized identity generated for
each accepted connection and must be unique in a snapshot. A reconnect must
expose a new identity and no stale entry for the prior connection. The
controller retains before-connect, two-connected, one-disconnected,
reconnected, and final-empty snapshots; one status response cannot prove that
lifecycle. The existing live-test envelope already carries PID, role, run token,
and request ID. The full assembly arrays, not only the abbreviated entries
above, must match the staged manifest exactly.

No runtime may work around a missing field by parsing console text or accepting
`registeredPlayers`.

## Evidence stability

The versioned evidence schema is
`dedicated-server-synthetic.evidence.v1`. It records both exact source
head/tree pairs, required checks, topology, manifest, timestamps, artifact
hashes, lifecycle snapshots, and the verdict.

The replay identity covers both repositories' source identities, profile,
seed, scenario options, expected controller IDs, the frozen manifest file hash,
and the authoritative module-validation contract. The state digest
contains only logical checks and stable protocol identity. Timestamps, PIDs,
paths, ports, run/request tokens, raw campaign ticks, passwords, and recyclable
connection IDs are excluded from the state digest.

The current head/tree arguments remain caller assertions until they match the
out-of-band frozen staged manifest. Evidence source identities are copied only
from that verified manifest, never from the arguments. The controller's pass
verdict remains one input to the source-bound orchestration receipt rather than
authorizing an unverified caller identity by itself.
