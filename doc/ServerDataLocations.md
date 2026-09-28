# Server data locations

This page lists where the co-op server looks for `mod-config.json`, the session `.json` next to each save and `steam-bans.json`, and which environment variables change those places. It describes the code on the `development` branch. Older builds differ; the log lines below show what yours does.

Only the server reads these files. Players who join get the server's mod options and do not need to set anything. A host who plays from the game normally sets none of the variables. The dedicated server's own files (`server-config.json`, its logs and the `--data-dir` option) are described in its `release-info.txt`.

## mod-config.json

The server uses the first folder that applies:

1. The folder named by `COOP_DATA_DIR`.
2. The folder named by `BANNERLORD_USER_DIR`. The file sits directly in it, with no `CoopData` subfolder.
3. The `CoopData` folder in the game's user folder, `Documents\Mount and Blade II Bannerlord\CoopData` on Windows. A dedicated server on the same account shares this file.

If the file is missing, the server creates it from the `mod-config.default.json` that ships with the mod. The file is read once per session, so edits apply to the next session. The server may edit it in place to add difficulty settings that older templates left out or shipped as comments. Values you set are kept.

An older `mod-config.json` directly in `Documents\Mount and Blade II Bannerlord` is not read.

The log shows the file in use: `mod-config.json loaded (<path>)`, after `created <path> from <template>` when the server had to create it. On a dedicated server, check this line to see which file your build reads.

## Saves and session files

`<save>.sav` holds the world. `<save>.json`, with the same name, holds each player's character and co-op progress. The server writes the `.json`:

- to the `Game Saves` folder inside the folder named by `BANNERLORD_USER_DIR`, when that variable is set;
- otherwise to the folder that holds the `.sav` files. When you host from the game, that is `Documents\Mount and Blade II Bannerlord\Game Saves` (under `OneDrive\Documents` when OneDrive backs up your Documents folder).

`COOP_DATA_DIR` does not change this folder. A dedicated server keeps its saves and their `.json` files in `Game Saves` inside its data directory.

On a PC that hosts from the game, `BANNERLORD_USER_DIR` moves the `.json` away from its `.sav`. The server then looks for each save's `.json` in the new folder, so returning players go to character creation, and bug reports leave out the session file. Set it only on a headless host whose saves are already in that folder.

Each time a player joins, the server also writes `TransferSave.json` next to the session files. A bug report writes a `coop_bug_report.sav` and `coop_bug_report.json` pair the same way as a save.

When the `.json` is missing, the log shows `Co-op session JSON was not found at <path>`. No line names the file when it is found, or when it exists but cannot be read.

## steam-bans.json

The server reads one file, the first that applies:

1. The file named by `COOP_STEAM_BAN_FILE`. This is the file itself, not its folder.
2. `steam-bans.json` in the folder named by `COOP_DATA_DIR`.
3. `..\..\..\server-data\steam-bans.json`, relative to the folder that holds the server program. This is used only when neither variable is set.

`BANNERLORD_USER_DIR` is not used. To move only the ban file, set `COOP_STEAM_BAN_FILE`, because `COOP_DATA_DIR` also moves `mod-config.json`. Keep the file outside the server's install folder.

The server never creates the file or its folder. A missing file logs nothing, and nobody is banned. The first time a Steam player joins after the server starts or the file changes, the log shows `Steam ban list loaded from <path> (<n> id(s))`. If `COOP_STEAM_BAN_FILE` names a folder, the list is never read and each Steam join logs `Steam ban list could not be reloaded from <path>`.

See [Steam bans](SteamBans.md) for the file format and which ids are loaded.

## Setting the variables

- Enter the path only, with no quotes and no spaces or line breaks before or after it. The server does not remove quotes, and a quoted path does not work.
- Use absolute paths. A relative path is resolved against the server's working directory.
- An empty variable counts as unset. Leave a variable unset rather than setting it to spaces.
- On Windows, set them as User variables of the account that runs the server, or as System variables. On Linux the names are case-sensitive.
- The server uses the values it was started with. Restart it after a change. When you host from the game, fully exit the game and Steam, then start them again.
- Setting, changing or removing a variable does not move your files. Copy what the variable moves to the new folder first: `mod-config.json` for either folder variable, `steam-bans.json` for `COOP_DATA_DIR`, and each save's `.json` for `BANNERLORD_USER_DIR`. Otherwise the server starts on template settings, nobody is banned, or returning players go to character creation.
- The server creates the folders for `mod-config.json` and the session `.json` when they are missing, and needs write access to them.
- In a container, point the variables at a mounted volume. Files in the container's own filesystem are lost when it is recreated.
