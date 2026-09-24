# Player list (#3642)

## Behavior

- F8 toggles the list on the campaign map. P remains the vanilla Party shortcut.
- Opening the list does not pause the campaign. Entering a settlement menu, mission, or another screen closes it.
- Membership comes from `IPlayerManager.Players`, including saved offline players. No second player registry is maintained.
- `Player.PlatformName` is optional saved metadata, refreshed from the client's platform service after campaign join. Older saves show `Unknown platform name` until that player joins again.
- A player appears when the existing backend registers them. A registered player without assigned game objects shows `Hero not assigned` / `Online · Connecting`.
- Activities are sampled on the server approximately once per real-time second. Travelling requires actual displacement between samples. Stationary and paused parties show Idle unless an encounter or settlement takes priority.
- Offline rows have no activity. Rows are reconciled by controller ID, not name, including when names are identical.

## Targeted automated checks

```powershell
dotnet test source/GameInterface.Tests/GameInterface.Tests.csproj -c Debug -p:Platform=x64 --no-restore --filter "FullyQualifiedName~PlayerListTests|FullyQualifiedName~PlayerActivityReaderTests|FullyQualifiedName~ReplacePlayer_CurrentRegistration"
dotnet test source/Coop.Tests/Coop.Tests.csproj -c Debug -p:Platform=x64 --no-restore --filter "FullyQualifiedName~PlayerListServerHandlerTests|FullyQualifiedName~ConnectedPlayerCountServerHandlerTests"
```

Result: 15 GameInterface tests and 4 Coop tests passed. Covers wire/JSON metadata, replacement, duplicate names, reconnect identity, late-join baseline, offline retention, unchanged snapshot suppression, settlement and encounter precedence, attached parties, movement sampling, and large roster data.

## Live checks performed

DEBUG build deployed with Windows MSBuild. Two MCP runs were stopped with cleanup confirmed for every owned process.

Run `03071f8ad50f46fd8759287323e88866` used server + clients 1 and 2:

- The restored `testclient` registration remained Offline.
- Client 2 showed its own platform name (`Joke`) and campaign hero (`RandomPlayer`). The dedicated server was absent.
- Joining client 1 updated the open list to three rows. Identical platform/hero names did not merge players.
- `coop.debug.connection.disconnect` on client 2 changed its existing row to Offline, with no stale activity.
- `coop.debug.connection.reconnect` after teardown restored that row to Online without duplication.

Run `cf5feccaeaf644fdb845a70fdb88892e` verified the movement correction:

1. Server: `coop.debug.players.list` resolved client 2 to `MobileParty_Player448` in this save.
2. Server: `coop.debug.mobile_party.move_to_settlement MobileParty_Player448 town_ES1 false` ordered movement to Danustica while paused.
3. Client 2: `coop.debug.player_list.toggle`, then `coop.debug.player_list.inspect`, showed `Online · Idle` despite the pending order.
4. Server: `coop.debug.set_time_mode Play_1x force-live-test`. The open list updated to `Online · Travelling` while the party actually moved.
5. Arrival at Danustica closed the overlay. Client inspection reported `Online · In Town`.
6. Server time was paused again before cleanup.

The two player-list debug commands are UI-only; world mutations originate on the server.

## Screenshot evidence

Artifacts are under `.mcp-local/runs/` (not committed):

- `03071f8ad50f46fd8759287323e88866/client1-7dc27881dd2640a59193a9916353e3b0.png`: retained disconnected row, platform name preserved, no stale activity.
- `cf5feccaeaf644fdb845a70fdb88892e/client2-bcb7a3cb27484bf2a76174d791190e48.png`: paused party with a movement order correctly shows Idle.
- `cf5feccaeaf644fdb845a70fdb88892e/client2-633095d6333748dc8af7b3a35ed63013.png`: actual movement shows Travelling. Some hero-column text is partially missing in this capture; visual follow-up remains.
- `cf5feccaeaf644fdb845a70fdb88892e/client2-712885ebbe704bb087ab5294064c33c5.png`: settlement menu, player list closed.

## Remaining manual coverage

Physical F8/Escape input, native close-button clicking, dragging/wheeling through the overflowing list, and live village/castle/battle/siege/hideout transitions have not all been exercised. MCP verified the shared toggle action, not synthetic hardware key presses. The data/precedence tests do not replace those checks.

## Reference-style visual pass

The panel now uses native frame, paper texture, scoreboard hover, activity sprites, scrolling text, and keyboard glyph components. The title and text brushes are custom XML; there are no custom widget classes or artwork. The three MVP columns and roster backend are unchanged.

Latest DEBUG build/deployment succeeded; the five `PlayerListTests` passed, including counter/offline styling updates and restoring the latest real roster after a layout preview.

Final MCP run: `3291ba3d9aea461c98e52d13882e77cc`. Screenshots were visually inspected at 1440×1080:

- `.mcp-local/runs/3291ba3d9aea461c98e52d13882e77cc/client2-87aee9d93099471590b35e694dd62556.png`: actual roster, readable title and columns, online count, subdued offline row, activity icons, compact Escape footer.
- `.mcp-local/runs/3291ba3d9aea461c98e52d13882e77cc/client2-7a4cf7a40b924b41ac8d1f74aebd7979.png`: 20 synthetic layout rows, long-name ellipses, visible scrollbar, row hover highlight and full-name tooltip. All online activity labels/icons are visible. This verifies presentation, not gameplay transitions.

No missing-name rendering was observed in these final captures. The earlier screenshot observation above applies to the previous styling.

To reproduce the layout-only preview on a DEBUG client:

1. `coop.debug.player_list.toggle` on the campaign map.
2. `coop.debug.player_list.preview on` to display synthetic rows without changing players, network state, or save data.
3. `coop.debug.player_list.preview off` to restore the latest server snapshot.

Restoration was checked before shutdown. All three styling test runs were stopped with cleanup confirmed for every owned process.

## Final vanilla polish and presence indicators

The vanilla frame/parchment styling is retained with cleaner text, gold headings, a boxed `Players Online: N` counter with a troop icon and a panel sized for two to eight rows before scrolling. Presence appears before the platform name: filled green circle online, hollow gray ring offline. Row tooltips include the localized presence label. Online status cells show activity only; offline cells still say Offline.

Five PlayerListTests passed, including online counts, height limits, and presence/activity changes on disconnect/reconnect. DEBUG build and deployment succeeded.

Visually inspected final presence screenshot at 1440x1080:
`.mcp-local/runs/4dc29b6e6a414a6c81ea017190ce9642/client2-bcaab8c0b83b43e9a0dc95d29bb820e6.png`.
Both filled and hollow indicators are distinct, names align with their heading, and status reads Offline / Idle without an Online prefix. The shape difference does not rely on color. No color-vision simulation was performed.

Earlier compact/overflow layout captures are under run `a7506cc69f5b44429207d89cdf539a27`, files `client2-4796100c540242718f2fb8c66579ff28.png` and `client2-08cc697545244eb0baff65c50154a21c.png`. These predate the presence indicators. All subsequent test runs were stopped with process cleanup confirmed.

Latest counter screenshot: [player list](../../Images/player-list.png). Captured in run `8d1d104ec5d24cec8eab460455ee9adf`; boxed counter, troop icon, presence indicators and activity-only status visually checked. Server/client cleanup confirmed. Final targeted rerun: 15 GameInterface tests and 4 Coop tests passed.
