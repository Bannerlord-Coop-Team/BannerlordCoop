# Server info

Servers can greet each player with a Server Info panel once their campaign has synced. It opens on the campaign map in the style of the player list, with up to four tabs: Message of the Day, Rules, Links and News. The player switches tabs by clicking them, closes the panel with Escape, the Close button or F3, and opens it again with F3 on the campaign map. Write it in the `serverInfo` block of `mod-config.json`:

```json
{
  "serverInfo": {
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
}
```

Every key is optional. Only tabs with content are shown, always in the order above, and the panel opens on the first of them. An unknown key in the block is logged and ignored, like elsewhere in the file.

- `motd`: one paragraph per entry.
- `rules`: one rule per entry. The panel numbers them 1., 2., ... in the order written.
- `links`: each entry has a `url` and an optional `label`. Each link shows its label with the full address under it. A link without a label shows its host, like `example.com`, as the label.
- `news`: each entry has an optional `date`, `title` and `text`, and needs at least a title or a text. The date is free text shown as written. Entries are shown in the order written, so put the newest first.

## Limits

The server applies these when it reads the file. Over a limit it keeps what fits and logs one warning naming the key.

| Key | Entries | Length |
|---|---|---|
| `motd` | 10 paragraphs | 1000 characters each |
| `rules` | 20 rules | 1000 characters each |
| `links` | 8 links | label 40 characters, address 512 characters |
| `news` | 10 entries | date 40, title 80 and text 1000 characters each |

Text is cut at the limit without splitting a character. Tabs, line breaks and other control characters become spaces and the ends are trimmed. Entries that are empty after that are skipped. A key that is not an array is skipped with a warning, and a value of the wrong shape inside a list (an object where a text belongs, a number where a link belongs) makes the whole file unreadable, so the server logs it with its path and runs on defaults, like any other malformed value in `mod-config.json`. All text is shown exactly as written, with no formatting and no `{...}` placeholders. Every player who joins sees it, so never put passwords or admin contacts in it.

## Links

A link is kept only if its address is an absolute `http://` or `https://` address with no user name or password (`user@host`), no spaces, backslashes, control or invisible characters, and at most 512 characters. Anything else, such as `javascript:`, `file:`, `ftp:` or a relative address like `/rules`, is dropped with a warning naming its index in the `links` array (the first entry is index 0) and never reaches a player. The log does not show the address.

The address a player sees is normalized: the scheme and host are lower case, a default port is dropped, the path is escaped, and a host with non-English letters is shown in its `xn--` form, so a name that only looks like another cannot pass for it. Each player's game checks every link again before showing it, and its log names the index of any link it drops, again without the address.

Clicking a link opens a small dialog in the panel that shows the full address. The browser opens only after the player clicks Open; Cancel or Escape closes the dialog and leaves the panel open. Open is dimmed and takes no click for half a second after the dialog appears, so a double click on a link cannot open it. If the browser does not start, the player's log names the kind of error and its code, never the address.

## Showing it again

The panel opens by itself once per join, when the player first reaches the campaign map with nothing else open, and does not pause the game. F3 on the campaign map opens it again on its first tab, and F3 on the open panel closes it. F3 does nothing in a battle, a settlement, a conversation or while typing in chat, and the player list and the panel never open over each other. A player who rejoins sees the panel again.

## Where it is read

`serverInfo` is part of `mod-config.json`, and [Server data locations](ServerDataLocations.md) says where the server reads that file. It is read once per session, so restart the server after editing it. No block or an empty block shows nothing. When there is something to show, the log has `Server info loaded from mod-config.json (motd <n> paragraph(s), rules <n>, links <n>, news <n>)`.
