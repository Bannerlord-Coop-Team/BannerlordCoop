## Problem statement

Automatically propagate runtime field/properties changes across networked clients


## Non-goals
- No full object replication
- Player and AI movement (this will be handled by a separate system)


## Supported use cases
- Mod environment, no source access


## Constraints
- Harmony / IL patching only
- Cannot modify target classes


## High-level flow
The server will propagate member updates (field/property) changes automatically to all connected clients. The clients will disable entire game behaviors using harmony prefixes that will be server owned (settlement, relations, etc…). When a server owned member is updated, the server updates the member locally with the new value, and commands all remote connections to update in the same way using a deterministic queue (deterministic by member updates update sequence).

All client related functionality will be handled with a RPC, the RPC is acted on in the server, and this system automatically propagates the changes. In the case the client attempts to change a server controlled member updates, that event shall be logged.

When a new client joins, the server shall pause the game and command all connected clients to pause, create a new save, transfer the new save over the network to the new client,  will have the save file sent to them, the NetworkIds will be sent using a key value pair `MBObject.StringId` -> `NetworkId` to assosiate the existing network Ids. Any discrepencies shall be logged. Failure to join after a certain amount of time will disconnect that client and drop any updates meant to be sent to that client. This event will also be logged and displayed to the server and other clients on the screen


