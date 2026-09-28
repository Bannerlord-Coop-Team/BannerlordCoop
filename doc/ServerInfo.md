# Server info

Dedicated servers can greet each player with a Server Info panel once their campaign has synced. It opens on the campaign map in the style of the player list, with up to four tabs: Message of the Day, Rules, Links and News. The player switches tabs by clicking them and closes the panel with Escape or the Close button. Write it in `server-info.json`:

```json
{
  "motd": [
    "Welcome to EU-1. Be kind in chat and keep battles fair.",
    "The server restarts every day at 06:00 UTC."
  ],
  "rules": [
    "Be respectful in chat and voice.",
    "No griefing, exploiting or duplicating items.",
    "Ask before joining another player's army."
  ],
  "links": [
    { "label": "Discord", "url": "https://discord.gg/example" },
    { "label": "Website", "url": "https://example.com/" }
  ],
  "news": [
    { "date": "28 Sep 2026", "title": "Siege weekend", "text": "All castle sieges this weekend give double renown." },
    { "date": "25 Sep 2026", "title": "Server updated", "text": "Update your mod to join." }
  ]
}
```

Every key is optional. A file with only `motd`, as older servers have, keeps working. Only tabs with content are shown, always in the order above, and the panel opens on the first of them. Unknown keys are ignored.

- `motd`: one paragraph per entry.
- `rules`: one rule per entry. The panel numbers them 1., 2., ... in the order written.
- `links`: each entry has a `url` and an optional `label`. A link without a label shows its address instead.
- `news`: each entry has an optional `date`, `title` and `text`, and needs at least a title or a text. The date is free text shown as written. Entries are shown in the order written, so put the newest first.

## Limits

The server applies these when it reads the file. Over a limit it keeps what fits and logs one warning naming the key; it never stops over them.

| Key | Entries | Length |
|---|---|---|
| `motd` | 10 paragraphs | 2000 characters together |
| `rules` | 20 rules | 2000 characters together |
| `links` | 8 links | label 40 characters, address 512 characters |
| `news` | 10 entries | date 40, title 80 and text 500 characters each |

All text together is limited to 12000 characters. The limits of `motd`, `rules` and `links` keep them under that, so only the last news entries can be dropped for it.

Text is cut at the limit without splitting a character, and nothing after a cut `motd` or `rules` entry is kept. Tabs, newlines and other control characters become spaces and the ends are trimmed. Entries that are empty after that, or are not text, are skipped. All text is shown exactly as written, with no formatting and no `{...}` placeholders. Every player who joins sees it, so never put passwords, admin contacts or internal addresses in it.

## Links

A link is kept only if its address is an absolute `http://` or `https://` address with a host, has no user name or password (`user@host`), no spaces, backslashes, control or invisible characters, and is at most 512 characters long. Anything else, such as `javascript:`, `file:`, `ftp:` or a relative address like `/rules`, is dropped with a warning naming its index in the `links` array (the first entry is index 0) and never reaches a player. The server log does not show the address.

The address a player sees is normalized: the scheme and host are lower case, a default port is dropped, the path is escaped, and a host with non-English letters is shown in its `xn--` form, so a name that only looks like another cannot pass for it. Each player's game checks every link again before showing it, and its log names the index of any link it drops, again without the address.

Clicking a link opens a small dialog in the panel that shows the full address. The browser opens only after the player clicks Open; Cancel or Escape closes the dialog and leaves the panel open. Open is dimmed and takes no click for half a second after the dialog appears, so a double click on a link cannot open it.

## Showing it again

The panel opens once per join, when the player first reaches the campaign map with nothing else open, and does not pause the game. A player who rejoins sees it again.

A player who types `!motd` in chat, alone and in any case, opens the panel again on its first tab. `!motd` is not sent to the server or other players. If this server sent nothing to show, a line in the player's own chat says so. Anything else, like `!motd please`, is sent as a normal chat message.

## Where the file goes

The server reads one file, the first that applies:

1. The file named by `COOP_SERVER_INFO_FILE`. This is the file itself, not its folder.
2. `server-info.json` in the folder named by `COOP_DATA_DIR`.
3. `..\..\..\server-data\server-info.json`, relative to the folder that holds the server program. This is used only when neither variable is set.

To move only this file, set `COOP_SERVER_INFO_FILE`, because `COOP_DATA_DIR` also moves `mod-config.json`.

The file is read once when the server starts, so restart the server after editing it. Without the file nothing is shown and nothing is logged. If the file holds malformed JSON, the server logs an error and runs without server info. The server log shows how many paragraphs, rules, links and news entries it loaded, not their text.
