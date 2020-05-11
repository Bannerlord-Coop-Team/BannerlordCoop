# Bannerlord Coop
Mod to enjoy the Mount & Blade II: Bannerlord campaign with other players.

## Current state
Very early in development. There's is absolutely no gameplay so far. [Video created on commit d86c24c](https://youtu.be/Y_htoMXQGqU).

Implemented:
- Establish a network connection between 2 game instances.
- Send the initial world state from host to client.
- Load the initial world state on client.
- Sync of time control (pause, play, fastforward) & main party movement (move to position only).

Next:
- Streamline harmony patches
- Evaluate feasibility of running all clients in lock step with synced RNG. It would require a lot less hooks into the currently very unstable game DLLs, but might end up in desync hell.
- Evaluate feasibility of a headless server (run only the game simulation, patch out everything else).

## How to build & deploy
1. Set the path to your Bannerlord modules folder in `config.json`.
2. Open the `Coop.sln` in Visual Studio.
3. Build.
4. If everything was successful, your Bannerlord mod folder now contains the Coop mod. Enable it through the launcher or launch directly from Visual Studio.

## How to host a coop game
1. Start the game with the Coop mod enabled.
2. Load any save game.
3. Open the console using the hotkey `` Ctrl + ` ``.
4. Execute the command `coop.start_local_server`.
5. `coop.info` will print some debug information about the server.
The game is now running a local coop server and is connected to it with a local client. Additional players can now join.

## How to join a coop game
1. Start the game with the Coop mod enabled.``
2. Open the console using the hotkey `` Ctrl + ` ``.
3. Execute the command `coop.connect_to 127.0.0.1 4201`. 
4. The client connect to server, download a save game and load it locally. It takes quite a long time until a loading screen pops up (~30s).
Only works in LAN, never tested it in WAN.

## Contributing
If anyone is interested in contributing to this project, please feel free to open an issue and request additional documentation & information. Until then i'm treating this as a personal project and will not actively communicate any issues, TODOs or design decisions.

## Acknowledgments
- Zetrith for [Multiplayer](https://github.com/Zetrith/Multiplayer).
- Salminar for [NoHarmony](https://github.com/Salminar/NoHarmony).
- ashoulson for [RailgunNet](https://github.com/ashoulson/RailgunNet).