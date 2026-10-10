# Co-op changes and restrictions

These are source observations at the implementation commit in [provenance](provenance.md),
not passing runtime claims. Installed baseline definitions remain separately visible in
[baseline/installed.json](baseline/installed.json). The [action map](player-actions.md) names remaining acceptance questions.

The [release-facing README](../README.md#current-features) separately advertises current
capabilities, marks armies/sieges as experimental and hideouts/quests/naval support as planned.
Its compatibility guidance excludes the War Sails DLC. These are release claims/restrictions,
not runtime proof of any action row. Installed naval menus do not override that restriction,
and one source-allowlisted issue does not make quests generally supported.

| Player surface | Repository-owned behavior or restriction | Source | Proof still required |
| --- | --- | --- | --- |
| Players sharing a campaign | server owns world progression; each player is a client with its own registered hero/party/clan; host is excluded from player count | [player inspection](../source/GameInterface/Services/Players/Commands/PlayerDebugCommands.cs), [MCP topology](../tools/CoopMcpServer/README.md) | both real client identities, admission, reconnect, saved registrations |
| Player objects | player inspection reports object resolution and membership in the controlled-object registry | [PlayerDebugCommands](../source/GameInterface/Services/Players/Commands/PlayerDebugCommands.cs) | actual constructor/registry ordering, creation/removal and late join |
| Mutable world data | auto-sync declarations identify fields/properties and write targets; receive paths and authoritative side effects remain distinct | [auto-sync](../source/GameInterface/AutoSync), [auto registry](../source/GameInterface/Registry/Auto), [invariants](../AGENTS.md) | source-specific serialized/apply ordering and object creation on both clients |
| Normal issues | one issue behavior is allowlisted; the 42 other registered vanilla types are gated from normal admission | [allowlist](../source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs) | actual gate application, normal dialogue and allowed quest branch evidence |
| Debug issue grants | factories list known types and can be wired/unwired independently of normal gameplay availability | [IssueGiveCatalog](../source/GameInterface/Services/Issues/Commands/IssueGiveCatalog.cs) | no compatibility claim from granting/completing through debug commands |
| Quest journal | `GameUIDisable` rejects `QuestsState` | [GameUIDisable](../source/GameInterface/Services/UI/Patches/GameUIDisable.cs) | rendered input/dialogue behavior; ownership/progress independently |
| Character, inventory, party, clan and kingdom states | these state pushes are allowed only on clients | [GameUIDisable](../source/GameInterface/Services/UI/Patches/GameUIDisable.cs) | client UI action and authoritative effects for each screen |
| Narrative incidents | `InvokeIncident` is suppressed | [IncidentDisable](../source/GameInterface/Services/UI/Patches/IncidentDisable.cs) | actual incident trigger observation; do not generalize to every notification |
| Campaign save controls | client Save, Save As and Save And Exit entries are replaced with disabled actions; `SetSaveArgs` is blocked on clients | [escape-menu controls](../source/GameInterface/Services/UI/Patches/EscapeMenuDisableSavePatch.cs), [save gate](../source/GameInterface/Services/Save/Patches/SaveHandlerClientBlockPatch.cs) | source-bound completed server save, sidecar continuity and reload |
| Save/session continuity | server save handler writes co-op session metadata and reports write result separately | [SaveGameHandler](../source/Coop.Core/Server/Services/Save/Handlers/SaveGameHandler.cs), [CoopSessionWritten](../source/Coop.Core/Server/Services/Save/Messages/CoopSessionWritten.cs) | session write success, durable campaign save and actual restored players |
| Inventory preferences | inventory locks/sort preferences are stored by player hero ID | [SessionInventoryPlayerDataInterface](../source/GameInterface/Services/Inventory/Interfaces/SessionInventoryPlayerDataInterface.cs) | two players' independent preferences and accepted trade/resource state |
| Campaign time | client/server time requests, pause leases and unpause/fast-forward policies are explicit services | [TimeControlInterface](../source/GameInterface/Services/Time/Interaces/TimeControlInterface.cs) | competing requests, menu/battle pause and disconnect policy |
| Tournament sessions | shared session/participant/choice state and client requests replace a single player's local tournament path | [tournament services](../source/GameInterface/Services/Tournaments), [starter recipe](recipes/tournament.md) | real lobby/UI, each round, spectating, betting, rewards and cleanup |
| Garrison backlink | `FiefSync` tracks the component field and the two lifecycle write methods | [FiefSync](../source/GameInterface/Services/Fiefs/FiefSync.cs), [starter recipe](recipes/garrison.md) | server lifecycle mutation and exact backlink observations on both clients |
| Co-op options | provider-owned tabs and section IDs persist network, time, player-list/nameplate, kill-feed and voice options | [options providers](../source/GameInterface/Services/UI/CoopOptions/Providers), [storage contract](../source/GameInterface/Services/UI/CoopOptions/README.md) | real Apply/cancel/persist behavior and each option's actual effect |
| Player list, chat and voice | repository-owned UI/communication services are present separately from baseline game menus | [player list](../source/GameInterface/Services/UI/PlayerList), [chat](../source/GameInterface/Services/Chat), [voice](../source/GameInterface/Services/Voice) | correct recipient/sender, live presentation/audio and disconnect behavior |
| Battles | co-op battle/session/authority/spawn services define additional ownership and lifecycle boundaries | [battle services](../source/Missions/Battles), [map-event services](../source/GameInterface/Services/MapEvents) | real mission actions, casualties, reinforcements, authority transition and campaign return |
| Naval paths | War Sails DLC is excluded by the release compatibility guidance; naval support is planned and no `Missions.Naval` project exists in this bound tree | [release guidance](../README.md#compatibility), [installed snapshot](baseline/installed.json), [mission tree](../source/Missions) | a definition does not authorize enabling an excluded DLC or establish gameplay support |

For each surface, query `python tools/feature_map.py find governor` for current commands,
sync expressions and Harmony target declarations, then inspect the named source before selecting a real action. The
lexical index does not resolve every dynamic target or establish that a patch/command is active.
The current allowlist could also fail to apply at runtime, an exception explicitly reported
by its source; the map must preserve contrary runtime evidence rather than override it.
