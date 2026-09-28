# Server info

Dedicated servers can greet each player with a short message of the day (MOTD) once their campaign has synced. It opens as a popup on the campaign map in the style of the player list, and the player closes it with Escape or the Close button. Write it in `server-info.json`:

```json
{
  "motd": [
    "Welcome to EU-1. Be kind in chat and keep battles fair.",
    "The server restarts every day at 06:00 UTC.",
    "Report problems in our Discord channel."
  ]
}
```

Each entry is one paragraph. The server keeps the first 10 non-empty paragraphs and at most 2000 characters in total, turns control characters such as tabs and newlines into spaces, and skips entries that are not valid text. The text is shown exactly as written, with no formatting or `{...}` placeholders. Every player who joins sees it, so never put passwords, admin contacts or internal addresses in it.

The popup opens once per join, when the player first reaches the campaign map with nothing else open, and does not pause the game. A player who rejoins sees it again.

The server reads one file, the first that applies:

1. The file named by `COOP_SERVER_INFO_FILE`. This is the file itself, not its folder.
2. `server-info.json` in the folder named by `COOP_DATA_DIR`.
3. `..\..\..\server-data\server-info.json`, relative to the folder that holds the server program. This is used only when neither variable is set.

To move only this file, set `COOP_SERVER_INFO_FILE`, because `COOP_DATA_DIR` also moves `mod-config.json`.

The file is read once when the server starts, so restart the server after editing it. Without the file nothing is shown and nothing is logged. If the file holds malformed JSON, the server logs an error and runs without a MOTD. The server log shows the number of paragraphs it loaded, not their text.
