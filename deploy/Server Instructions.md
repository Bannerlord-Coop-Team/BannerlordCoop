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

---
### 2. Disable Warsails DLL if you have it


### 3. Create the Initial Campaign Save

When starting a server for the first time, you need to create a campaign save before hosting.

From the main menu:

1. Select **Sandbox**.
2. Create a new character.
3. Load into the campaign map.
4. Save the game.
5. Exit back to the main menu.
6. Select **Host Coop Campaign**.

---

### 4. Port Forward UDP Ports

The host needs to forward the following UDP ports on their router:

```text
4200-4201 UDP
```

Forward these ports to the local IP address of the computer running the server.

Example:

```text
Protocol: UDP
Ports:    4200-4201
Target:   192.168.1.105
```

The exact router steps depend on your router model.

---

### 5. Start the Server

Navigate to the mod folder:

```text
Modules/Coop
```

Run:

```text
start-server
```

This will start the Coop server.

---

### 5. Connecting

Players on the same LAN can connect using the host machine’s local IP address, for example:

```text
192.168.1.105
```

Players connecting over the internet should use the host’s public IP address or domain name.

The server must remain running while players are connected.