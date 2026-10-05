# Source provenance and limits

Discovery date: 2026-10-05. Repository: `Bannerlord-Coop-Team/BannerlordCoop`.
Bound implementation commit: `5f4d63cc0975174ab42cfe1da67fc6d7214065e7`.
Implementation tree: `9fc0c1eb1986a7c6d86549dadccd324280610431`.
The main test checkout's unrelated edits were excluded by using an isolated worktree of
`origin/development`. No game build, deployment, save change or gameplay run was performed.

Installed Native reports `v1.4.8`. Managed discovery used complete-type decompilation, followed
by complete, untruncated bounded reads of the relevant registrar/perk/menu source. Returned
source ranges were checked for truncation, continuity and current-build match; a truncated
tool response was reread at smaller ranges. Symbol/parameter records are retained, not the
decompiled game implementation. [baseline/installed.json](baseline/installed.json) names each type,
registrar, line and immutable artifact identity.

| Owning installed assembly | SHA-256 |
| --- | --- |
| `bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll` | `1f8e33e2ed73e6ec653d7629180afb70649ddc6e5bd1657a802a264efda1c3ae` |
| `Modules/SandBox/bin/Win64_Shipping_Client/SandBox.dll` | `587c6e94c425745c1478f632f3b66226ff8298666cb27e8e6d9a62488df5956a` |

Inspected installed registrars are `TaleWorlds.CampaignSystem.SandBoxManager`,
`SandBox.SandBoxSubModule`, `DefaultPerks`, `EncounterGameMenuBehavior`,
`PlayerTownVisitCampaignBehavior` and `VillageHostileActionCampaignBehavior`.
The last four are in the CampaignSystem assembly; `DefaultPerks` is in its
`CharacterDevelopment` namespace, and menu registrars in `CampaignBehaviors`.
Focused oracles also inspect complete `GarrisonPartyComponent.OnInitialize` and `OnFinalize`
members and selected members of `GangLeaderNeedsToOffloadStolenGoodsIssueBehavior` in that same
CampaignSystem assembly. The full artifact ranges are hash-validated in the receipt; only the
listed quest members were read as complete semantic evidence.

The current-build evidence validation receipt is retained in
[installed-sources.json](evidence/installed-sources.json). It validates artifact identity,
range and source hash, not semantic correctness, runtime loading, licensing or module activation.
For a refresh, discover the exact type in the installed managed index, decompile the entire type,
read its relevant complete member/ranges, retain the logical DLL identity/hash/version and replace
only records established by those bytes. Treat a changed hash as different evidence.

Repository declarations are regenerated from `git ls-files` in this checkout only.
Each inventory record carries its owning repository file SHA-256 and source line. Comments,
test assemblies, package/output directories and the game installation are not harvested as
production declarations. Dynamic patch targets, inherited registrations, multiline/dynamic
command metadata and conditional builds are not exhaustively resolved by the lexical index.
Source line/hash freshness is checked by regenerating in memory and comparing.

Evidence labels:

- `candidate`: an acceptance question requiring exact installed behavior/source expansion.
- `source-inspected`: the named repository surface was inspected; no runtime claim.
- `installed-definition` / `installed-registration`: a symbol, parameter or registration in the named game DLL.
- `declaration-only`: an indexed co-op declaration; no assertion that DI, Harmony or the live command registry activates it.
- `disabled-by-issue-gate`: excluded by the current normal issue allowlist, even if a debug factory exists.
- `allowlisted-source-only`: permitted by the source allowlist; end-to-end gameplay remains unverified.
- `exercised`: only the exact named action and oracle in the retained result, on the named source/environment.
- `blocked`: a missing required prerequisite or executor is recorded rather than waived.

The `support` field records restrictions/unknowns independently of evidence status. Definitions
are not advertised as co-op compatibility. The 212 registrations do not cover all game systems,
and 165 options from three menu registrars do not cover every menu, dialogue or UI screen.
Perk role/bonus declarations omit consumer models and troop-use conditions from the readable
index; inspect those before predicting an effect. StoryMode, custom battle, native multiplayer,
native callbacks/physics and optional DLC modules need their own discovery and runtime evidence.
The bound tree has no `Missions.Naval` project; installed naval menu declarations remain unknown
co-op support. Candidate naval actions must not be inferred as enabled.
