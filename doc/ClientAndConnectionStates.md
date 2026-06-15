# Client & Connection State Machines

Bannerlord Coop drives the join/play lifecycle with two cooperating state machines:

- **ClientStates** — runs on the joining client ([`ClientLogic`](../source/Coop.Core/Client/ClientLogic.cs), states under [`Coop.Core/Client/States`](../source/Coop.Core/Client/States)). One per client.
- **ConnectionStates** — runs on the server, **one instance per connected client** ([`ConnectionLogic`](../source/Coop.Core/Server/Connections/ConnectionLogic.cs), states under [`Coop.Core/Server/Connections/States`](../source/Coop.Core/Server/Connections/States)).

Both implement a simple `SetState<T>()` pattern: the `Logic` object holds the current state, forwards interface calls to it, and each state decides which transition to perform (usually in response to a `Network*` message arriving over LiteNetLib). Transitions labelled `Network…` below are messages received over the wire; the rest are local `Logic.*()` action calls.

---

## ClientStates (client side)

```mermaid
stateDiagram-v2
    [*] --> MainMenuState

    MainMenuState --> ValidateModuleState : Connect → NetworkConnected, PatchAll → ValidateModules()

    ValidateModuleState --> CharacterCreationState : NetworkClientValidated (no hero) → StartCharacterCreation
    ValidateModuleState --> ReceivingSavedDataState : NetworkClientValidated (hero exists) → LoadSavedData()
    ValidateModuleState --> [*] : NetworkModuleVersionsValidated (mismatch) → Disconnect / Finalize

    CharacterCreationState --> ReceivingSavedDataState : NetworkHeroRecieved → LoadSavedData()
    CharacterCreationState --> MainMenuState : MainMenuEntered → Finalize

    ReceivingSavedDataState --> LoadingState : NetworkGameSaveDataReceived (load save) → LoadSavedData()
    ReceivingSavedDataState --> MainMenuState : Disconnect

    LoadingState --> CampaignState : CampaignReady (register, switch hero) → EnterCampaignState()

    CampaignState --> MissionState : MissionStateEntered
    CampaignState --> [*] : MainMenuEntered → Finalize

    MissionState --> CampaignState : CampaignStateEntered
    MissionState --> MainMenuState : MainMenuEntered → Finalize
```

| State | Waits for | On success |
|-------|-----------|------------|
| `MainMenuState` | `NetworkConnected` | Patch game, validate modules → `ValidateModuleState` |
| `ValidateModuleState` | `NetworkModuleVersionsValidated`, `NetworkClientValidated` | Branch on whether a hero already exists |
| `CharacterCreationState` | `CharacterCreationFinished`, `NetworkHeroRecieved` | Send new hero, then receive saved data |
| `ReceivingSavedDataState` | `NetworkGameSaveDataReceived` | Load host save → `LoadingState` |
| `LoadingState` | `CampaignReady` | Register objects, switch to player hero → `CampaignState` |
| `CampaignState` | `MissionStateEntered` | Enter battle → `MissionState` |
| `MissionState` | `CampaignStateEntered` | Return to map → `CampaignState` |

---

## ConnectionStates (server side, per client)

```mermaid
stateDiagram-v2
    [*] --> ResolveCharacterState

    ResolveCharacterState --> CreateCharacterState : NetworkClientValidate (no player) → CreateCharacter()
    ResolveCharacterState --> TransferSaveState : NetworkClientValidate (player exists) → TransferSave()

    CreateCharacterState --> TransferSaveState : NetworkTransferNewHero (create + register) → TransferSave()

    TransferSaveState --> LoadingState : Load() (save packaged & sent)

    LoadingState --> CampaignState : NetworkPlayerCampaignEntered → EnterCampaign()

    CampaignState --> MissionState : NetworkPlayerMissionEntered → EnterMission()

    MissionState --> CampaignState : NetworkPlayerCampaignEntered → EnterCampaign()
```

| State | Waits for | On success |
|-------|-----------|------------|
| `ResolveCharacterState` | `NetworkModuleVersionsValidate`, `NetworkClientValidate` | Validate modules, then branch on whether the player exists |
| `CreateCharacterState` | `NetworkTransferNewHero` | Unpack/register new hero, broadcast it → `TransferSaveState` |
| `TransferSaveState` | (constructor) | Pause time, save current game, send packet → `LoadingState` |
| `LoadingState` | `NetworkPlayerCampaignEntered` | Client is on the map → `CampaignState` |
| `CampaignState` | `NetworkPlayerMissionEntered` | Client entered battle → `MissionState` |
| `MissionState` | `NetworkPlayerCampaignEntered` | Client back on map → `CampaignState` |

---

## How the two machines interlock

The client and its server-side connection advance in lock-step by exchanging messages. The same handshake described above, viewed across the wire:

```mermaid
sequenceDiagram
    participant C as Client (ClientStates)
    participant S as Server (ConnectionStates)

    Note over C: MainMenuState
    Note over S: ResolveCharacterState
    C->>S: NetworkModuleVersionsValidate
    S-->>C: NetworkModuleVersionsValidated
    C->>S: NetworkClientValidate
    S-->>C: NetworkClientValidated

    alt Hero does not exist
        Note over C: CharacterCreationState
        Note over S: CreateCharacterState
        C->>S: NetworkTransferNewHero
        S-->>C: NetworkHeroRecieved
        S-->>C: (NetworkNewPlayerHeroCreated to other clients)
    end

    Note over S: TransferSaveState
    S-->>C: GameSaveDataPacket
    Note over C: ReceivingSavedDataState → LoadingState
    Note over S: LoadingState

    Note over C: CampaignReady → CampaignState
    C->>S: NetworkPlayerCampaignEntered
    Note over S: CampaignState

    Note over C,S: Battles toggle both sides between Campaign ↔ Mission
    C->>S: NetworkPlayerMissionEntered
    Note over S: MissionState
```

### Transition cheat-sheet

| Step | Client message out | Server message out |
|------|--------------------|--------------------|
| Module check | `NetworkModuleVersionsValidate` | `NetworkModuleVersionsValidated` |
| Client/hero check | `NetworkClientValidate` | `NetworkClientValidated` |
| New character | `NetworkTransferNewHero` | `NetworkHeroRecieved` (+ `NetworkNewPlayerHeroCreated` to peers) |
| Save transfer | — | `GameSaveDataPacket` |
| Joined map | `NetworkPlayerCampaignEntered` | — |
| Entered battle | `NetworkPlayerMissionEntered` | — |
