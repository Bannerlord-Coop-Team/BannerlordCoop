# Steam bans

Dedicated servers can refuse a player before their character is resolved or restored. Create `steam-bans.json` in the directory named by `COOP_DATA_DIR`:

```json
{
  "steamIds": [
    "76561198000000042"
  ]
}
```

Only numeric Steam64 identities between 5 and 20 digits are loaded. The file is checked on each join and reloaded when it changes, so adding or removing a ban does not require a server restart. If an edit contains malformed JSON, the server logs the error and keeps the last valid list.

The dedicated-server layout also supports `server-data/steam-bans.json` beside the engine directory. Set `COOP_STEAM_BAN_FILE` to an absolute path to choose a different location.

An optional `entries` array can retain display names for administration tools:

```json
{
  "steamIds": [
    "76561198000000042"
  ],
  "entries": [
    {
      "steamId": "76561198000000042",
      "heroName": "Example player"
    }
  ]
}
```
