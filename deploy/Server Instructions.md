## Starting the Coop Server

### 1. Unblock DLLs

After downloading or extracting the mod, Windows may block some DLL files.

To unblock them:

1. Open the `Modules/Coop` folder.
2. Right-click each downloaded `.dll` file.
3. Select **Properties**.
4. If you see an **Unblock** checkbox, check it.
5. Click **Apply**, then **OK**.

You may need to do this for DLLs inside subfolders as well.

**How to Unblock Files**

1. **Open PowerShell as Administrator**
   - Click the **Start** button.
   - Type **PowerShell**.
   - Right-click **Windows PowerShell**.
   - Select **Run as administrator**.

2. **Run the unblock command**
   - Copy and paste the command below.
   - Replace the folder path inside the quotes with the actual location of your folder.

```powershell
Get-ChildItem "C:\Path\To\Your\Folder" -Recurse | Unblock-File
```

---

### 2. Turn Off War Sails and the Optional Official Modules

Co-op does not support War Sails yet, and it refuses anyone who has any of these modules turned on. If they are installed, the host and every player turn them off in the launcher’s mod list before hosting or joining:

- **War Sails** (NavalDLC)
- **Birth and Aging Options** (BirthAndDeath)
- **Fast Mode** (FastMode)

Join errors use the name in parentheses.

---

### 3. Create the Initial Campaign Save

When starting a server for the first time, you need to create a campaign save before hosting. Do this after step 2, so the campaign is created without War Sails.

From the main menu:

1. Select **Sandbox**.
2. Create a new character.
3. Load into the campaign map.
4. Save the game.
5. Exit back to the main menu.
6. Select **Host Co-op Sandbox**.
7. Pick the save, then choose who can find the server in **Steam Lobbies**:
   - **Public**: everyone.
   - **Friends Only**: your Steam friends.
   - **None**: not listed, but Steam invites and direct IP joins still work.
8. Set an optional password, or leave it blank, and select **Host**.

**Keep each save’s .json with it**

When you host from the game, saves are in `Documents\Mount and Blade II Bannerlord\Game Saves` (under `OneDrive\Documents` when OneDrive backs up your Documents folder). Every save made while hosting gets a `.json` file with the same name next to its `.sav`. The `.json` holds each player’s character and co-op progress.

- Copy, move or back up the `.sav` and the `.json` together. Steam Cloud only copies the `.sav`.
- If you delete a save, delete its `.json` too.

---

### 4. Port Forwarding (Direct IP Joins Only)

Players who join through **Steam Lobbies** or a Steam invite usually do not need port forwarding.

For direct IP joins over the internet, the host needs to forward one UDP port on their router:

```text
4200 UDP
```

Forward it to the local IP address of the computer running the server. Players do not need to forward anything.

Example:

```text
Protocol: UDP
Port:     4200
Target:   192.168.1.105
```

The exact router steps depend on your router model. If port forwarding does not work on your connection, host through Steam instead.

If Windows asks whether Bannerlord (it can show as BannerlordStarter) may use the network, tick the network type your PC is on and allow it. Allowing it needs an administrator account. The prompt can open behind the game, so press Alt+Tab if you do not see it. If you use other security software, allow Bannerlord there too.

---

### 5. Connecting

Players select **Join Co-op Sandbox**, open **Direct** and type the host’s address in **Server Address**. Replace whatever is already in the box and leave the port out: 4200 is used when no port is given. If the host set a password, type it in **Password**.

Players on the same LAN can connect using the host machine’s local IP address, for example:

```text
192.168.1.105
```

Players connecting over the internet should use the host’s public IPv4 address (four numbers with dots, such as 203.0.113.5) or domain name.

The server must remain running while players are connected. When the server opens in its own window, closing your game does not stop it. Close the server window when you finish, and before hosting another save.

---

### If a Player Cannot Join

A player can see these while joining. Problems after joining are bugs, so please report them on Discord or GitHub with your logs.

- The game stays on **Connecting to Coop Server** with "Contacting the server...": the server cannot be reached. Check the address, the port forward and the firewall, then select **Cancel**.
- "Could not reach the co-op host through Steam...": check that the host’s server is still running, then try again.
- "ERROR: Enter a valid server address with an optional port": type only the address, such as 203.0.113.5. A port range such as 4200-4201 is not accepted.
- "The server password is incorrect.": passwords are case-sensitive. This also shows when the password box is left empty.
- "Incompatible co-op mod build ... Update the co-op mod on both sides." or "Timed out waiting for the server to validate the connection. The server may be running an incompatible version of the mod.": everyone needs the same co-op mod build.
- "Wrong game version detected. ...": everyone needs the same game version.
- "DLC is not supported. Please disable the following module(s): ...": turn off the named module (step 2).
- "To join the server the module ... is required.", "Wrong version of module ..." or "Server does not support module ...": everyone needs the same mods and versions, and it is best to run no other mods. If the module is NavalDLC, the host turns War Sails off (step 2).
- **Steam Lobbies** shows the server as **Incompatible**: the host uses a different co-op mod build. Hover over it to see both versions. A **Compatible** server can still refuse a player for the reasons above.