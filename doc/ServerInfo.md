# Server info

Dedicated servers can greet each player with a short message of the day (MOTD) once their campaign has synced. Create `server-info.json` in the directory named by `COOP_DATA_DIR`:

```json
{
  "motd": [
    "Welcome to EU-1",
    "Restart 06:00 UTC"
  ]
}
```

Each line reaches only the player who joined, as a System chat line, on every successful join or rejoin. The first 5 non-empty lines are used, control characters such as tabs and newlines become spaces, and a line longer than 256 characters is cut. Every player who joins sees these lines, so never put passwords, admin contacts or internal addresses in them.

The file is read once when the server starts, so restart the server after editing it. Without the file nothing is sent. If the file holds malformed JSON, the server logs an error and runs without a MOTD. The server log shows the number of lines it loaded, not their text.

The dedicated-server layout also supports `server-data/server-info.json` beside the engine directory. Set `COOP_SERVER_INFO_FILE` to an absolute path to choose a different location.
