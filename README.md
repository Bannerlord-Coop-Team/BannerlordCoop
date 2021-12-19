# Bannerlord Coop

## Summary

***The Bannerlord Coop mod is not playable in its current state. We will make an announcement in Discord/Reddit when we're ready for the community to begin bug hunting.***

Mod to enjoy the original Mount & Blade II: Bannerlord campaign with other players. Our intent is to keep true to the original code as much as possible.

---

## Social Links
[Discord](https://discord.gg/VXqGyT8)

[Reddit](https://www.reddit.com/r/BannerlordCoop/)

---

## Current State
***There's is absolutely no gameplay so far***.

Currently utilizing Bannerlord v1.5.9

Very early in development.  [Video created on commit d86c24c](https://youtu.be/Y_htoMXQGqU).

### Implemented:
- Establish a network connection between multiple game instances.
- Send the initial world state from host to client.
- Load the initial world state on client.
- Sync of time control (pause, play, fastforward) & main party movement (move to position only).

### Next:
- Compatability changes with the release of Bannerlord v1.6
    - Changes to Save/Load logic, including sending game data to clients
    - Create a custom Object Manager for synchronization use
    - Fix references from old Object Manager 
- Implement Synchronization framework across all required types
    - Mobile Parties, Caravans, Settlements, etc
    - Includes deferring RNG decisions to the server, then send decisions to all connected clients
- Evaluate feasibility of a headless server (run only the game simulation, patch out everything else).
- Implement Battles
    - Currently only planning for 1 party - we'll look in to reinforcements later
    - Must sync all equipment, inputs, locations, etc across all participating clients
- Compatability changes with the release of Bannerlord v1.7

---

## How to Build & Deploy
[See our website for setup instructions](https://bannerlord-coop-team.github.io/BannerlordCoop/overview/project_setup.html)

## How to host a Coop Server
If you haven't already, run Bannerlord normally and create a new game with the name `"MP"`.
This is just temporary for development. Creating a new screen to handle game selection is in our backlog.

1. Build and Run the 'Coop' project in Visual Studio.
3. Click on the new `Host Co-op Campaign` button on the main menu.
4. This will load the game and host the server.
    - Currently hard coded to `127.0.0.1:4201`. We've only developed on the same machine however external connections should be possible now if on LAN or with port forwarding.
5. This Server instance will not have a player party. Ideally we would run this instance headless without a UI but we're still investigating this possibility. 

The game should now be running a local coop server. Additional players can now join.

## How to join a coop game
1. Either open another instance of Visual Studio or edit your build configuration to build and deploy both the `Coop` and `ClientDebug` projects.
2. Click on the new `Join Co-op Campaign` button on the main menu in the client instance.
    - This will automatically attempt to join a server hosted at `127.0.0.1:4201`
        - This is currently hardcoded [NetworkConfiguration.cs](https://github.com/Bannerlord-Coop-Team/BannerlordCoop/blob/development/source/Network/Infrastructure/NetworkConfiguration.cs#L15). If you wish to change the address you can modify it here. We will allow users to change this once we create the UI.
4. The Client should now connectto your local server (Check the server chat log for connection updates)
5. In `DEBUG` mode, the client will skip to the end of character creation and will be dropped in the training field per the beginning of a campaign. 
    - Eventually we will have the server store save data for each client and this won't be required.
6. Note that the client cannot unpause the game - it must be done via the Server. You should be able to move your player and see the same movement synced across all other instances (including the Server).

---

## Contributing
If anyone is interested in contributing to this project, post in [#contributor-application](https://discord.gg/x7nzbHbpzv) in our discord. We will add you to the Bannerlord GitHub Team where you can then begin your development.

---

## FAQ
Please read through [the #faq channel in our discord server](https://discord.gg/VXqGyT8).

---
## Acknowledgments
- Zetrith for [Multiplayer](https://github.com/Zetrith/Multiplayer).
- Salminar for [NoHarmony](https://github.com/Salminar/NoHarmony).
- ashoulson for [RailgunNet](https://github.com/ashoulson/RailgunNet).
