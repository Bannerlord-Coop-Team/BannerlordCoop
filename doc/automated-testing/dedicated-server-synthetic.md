# Dedicated-server synthetic verification

The `dedicated-server-synthetic` tier is a bounded functional check between
BannerlordCoop and the native dedicated-server host. It is intended to cover the
real process, real UDP transport, connection lifecycle, and the small pre-save
join protocol without launching rendered clients.

This implementation is intentionally incomplete and fail-closed. The offline
wire lab and control-pipe preflight exist, but the evidence verdict is always
`blocked` until a later controller launches and drives the real dedicated server.
The current dedicated-server repository also lacks the first-class connection
roster required to distinguish connected peers from historical player
registrations.

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
2. Connect two distinct synthetic peers.
3. Observe a channel-0 Sequenced campaign-time heartbeat.
4. Send an intentional co-op build mismatch and observe an explicit denial.
5. Send a fresh deterministic controller ID and observe
   `NetworkClientValidated(HeroExists=false, Player=null)`.
6. Mark that result `protocolShortcut=true`; it proves only the pre-character
   validation response.
7. Disconnect and reconnect through the future real-process controller, then
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
  --scenario baseline|wrong-password \
  --port 4201 \
  --timeout-ms 5000 \
  --run-token ds-synthetic-run \
  --request-id ds-synthetic-request \
  --password-env BANNERLORD_COOP_TEST_PASSWORD \
  [--controller-id ds-synthetic-client-a] \
  --expected-clients 2
```

`--expected-clients` belongs to the server node. The baseline server requires
exactly 2; the isolated wrong-password server requires exactly 1. Client nodes
require one of the two fixed controller IDs and reject `--expected-clients`.

The password is read only from the named environment variable. It is never put
in process arguments, node JSON, evidence JSON, error text, hashes, or replay
identity. The wrong-password probe derives a bounded non-secret wrong value and
does not print it.

## Real-process preflight command

The controller connects to the existing opt-in live-test pipe for the exact
dedicated-server PID. It does not start Bannerlord or the dedicated server.

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
  [--timeout-ms 5000] \
  [--seed 0] \
  [--output evidence.json]
```

The response must match the request ID, PID, `server` role, and run token. It
must report `serving=true`, the exact join port, and an exact two-client
connection roster. A `registeredPlayers` count is explicitly rejected because
it cannot prove which network connections are current.

Even if all preflight fields validate, this foundation sets
`runtime-scenario-executed=false`, emits verdict `blocked`, and exits with code
8. It cannot produce passed evidence before the real-process scenario controller
exists.

## Required dedicated-server repository change

A separate BannerlordCoop.DedicatedServer change must extend the opt-in
live-test `status` result with authoritative fields shaped as follows:

```json
{
  "serving": true,
  "joinPort": 4201,
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
must be the resolved port the co-op server actually bound. `connectionRoster`
must come from current connection objects, not `IPlayerManager.Players`; fresh
synthetic controllers have not registered heroes. Disconnected entries must be
removed. `connectionInstanceId` is an opaque, sanitized identity generated for
each accepted connection and must be unique in a snapshot. A reconnect must
expose a new identity and no stale entry for the prior connection. The future
controller must retain before-connect, two-connected, one-disconnected,
reconnected, and final-empty snapshots; one status response cannot prove that
lifecycle. The existing live-test envelope already carries PID, role, run token,
and request ID.

Until that dedicated-server change is present, the exact blocking failure is
`blocked-on-dedicated-server-roster-surface`. No runtime should be launched to
work around it by parsing console text or accepting `registeredPlayers`.

## Evidence stability

The versioned evidence schema is
`dedicated-server-synthetic.evidence.v1`. It records both exact source
head/tree pairs, required checks, topology, manifest, timestamps, artifact
hashes, and the blocked reason.

The replay identity covers both repositories' source identities, profile,
seed, scenario options, expected controller IDs, and manifest. The state digest
contains only logical checks and stable protocol identity. Timestamps, PIDs,
paths, ports, run/request tokens, raw campaign ticks, passwords, and recyclable
connection IDs are excluded from the state digest.

The current head/tree arguments are caller assertions. A passing future runtime
controller must corroborate them against the staged bill of materials and the
running server's build version, assembly MVID, and executable/stage hashes before
recording runtime evidence. The present foundation cannot pass, so it does not
use caller-provided identities to authorize a runtime verdict.
