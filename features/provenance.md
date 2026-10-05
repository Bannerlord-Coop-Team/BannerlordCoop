# Source provenance and limits

Discovery date: 2026-10-05. Repository: `Bannerlord-Coop-Team/BannerlordCoop`.
Bound implementation commit: `5f4d63cc0975174ab42cfe1da67fc6d7214065e7`.
Implementation tree: `9fc0c1eb1986a7c6d86549dadccd324280610431`.
An isolated worktree excluded the main test checkout's unrelated edits. The map changes no
implementation, game build/deployment, save or gameplay runtime.

Installed Native reports `v1.4.8`. Complete-type decompilation and bounded complete-member
reads supplied the source interpretations and definitions. Truncated responses were reread;
only symbol/parameter records and paraphrased outcomes are packaged, not decompiled implementation.

| Owning installed assembly | SHA-256 |
| --- | --- |
| `bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll` | `1f8e33e2ed73e6ec653d7629180afb70649ddc6e5bd1657a802a264efda1c3ae` |
| `Modules/SandBox/bin/Win64_Shipping_Client/SandBox.dll` | `587c6e94c425745c1478f632f3b66226ff8298666cb27e8e6d9a62488df5956a` |

[installed.json](baseline/installed.json) binds the inspected `SandBoxManager`,
`SandBoxSubModule`, `DefaultPerks`, `EncounterGameMenuBehavior`,
`PlayerTownVisitCampaignBehavior` and `VillageHostileActionCampaignBehavior` records.
[installed-sources.json](evidence/installed-sources.json) retains the read-only analysis app's
artifact identity/range/hash validation. That receipt covers only its named original ranges,
not semantic correctness, runtime loading or every later source read.

[behavior-sources.json](baseline/behavior-sources.json) keeps the eight complete source
identities used by [source-cases.csv](source-cases.csv): six installed types and two repository
production files. It records logical owning DLL/version/hash, normalized complete-source digest
and line count; repository files have separate byte hashes. The native fallback used existing
focus-safe `ilspycmd` / `ICSharpCode.Decompiler` version `10.1.0.8386`. Both installed DLL hashes
were rechecked after discovery and matched. Span checks use these frozen source identities;
`check` does not inspect a current game installation or re-prove the source interpretation.

The historical [expanded-sources.json](evidence/expanded-sources.json) records the earlier
expanded discovery, including 175 installed types and inventories subsequently removed from
the compact map. Its counts and digests describe documentation revision
`a8900b80dca176b125c2f8f13e8dddfa263d4f59`, not current files. It is a native authoring receipt,
not app validation or a gameplay result. The original datasets remain in that Git revision.

Repository declaration inventories are generated on demand from `git ls-files` in this checkout.
They exclude comments, tests, packages, output directories and game installation files.
Each record names its current file hash/line. Lexical discovery does not exhaust inherited,
multiline/dynamic registration, conditional builds or actual DI/Harmony/command activation.
Installed definition records always remain tied to the frozen snapshot's DLL hash/version.

`candidate`, `source-inspected`, installed definitions and `source-interpreted` establish
different source facts; runtime and support restrictions stay independent as explained in
[granularity](granularity.md). `allowlisted-source-only` does not establish complete quest
support, and a debug catalog never enables `disabled-by-issue-gate` behaviors.
[Authoring evidence](evidence/authoring.md) covers map checks and the existing selector's exact
planning scope only. Four game recipes and all interpreted source cases remain unrun.

The snapshot is not game-wide coverage. Native callbacks/physics, StoryMode, custom battle,
native multiplayer and optional DLC modules require separate discovery and runtime evidence.
The bound tree contains no `Missions.Naval` project; installed naval menu definitions and
candidate actions must not be inferred as enabled co-op gameplay.
