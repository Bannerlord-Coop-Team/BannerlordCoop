# 76a5004fcfad: SCRIPT/harness-assumption, uncharged, budget 2/3

Supervisor disposition at 12:40Z supersedes the provisional UNCLASSIFIED assessment below. The raw journal graph matches end to end. The headless dedicated server does not register the native language functions, including PLURAL, and therefore renders a different server-only string. Owner clients render their journals locally. This is a harness assumption, not a #3957 product regression. Classify SCRIPT/harness-assumption with no budget charge; keep 2/3. The original automatic result remains immutable.

For server-mirror journal comparisons only, compare the complete raw TextObject graph (template and recursively typed variables, quantities and item identities), retaining all other native journal metadata and identity checks. Client-to-client rendered-text comparisons remain strict. Do not register PLURAL in the fixture or normalize text. Dedicated-server localization is a separate finding outside these PRs. A new job on the same 67496 head must still execute removal, ending, archive and both rejoins; none is established by this failed run.

The investigation below records the measured evidence and its historical limits before that supervisory disposition. Its cautions against speculative fixes remain applicable; the uncharged harness correction above is now explicitly authorized.

Exact source 67496e5da53fe30206f3a8cf1d3cb82b78678800, tree 58eb278be0d6e10fab54a724418c0f8ccb67c51b. Parent independently verified all 290 result-manifest file hashes, sizes and exact file set. Result: 20 PASS / 1 FAIL / 96 UNRUN. Step 21 timed out comparing the accepted owner's journal with its server mirror.

The only captured packet difference is `$.entries[0].text`: owner says "2 loads of wrought iron", server says "2 Wrought Iron". Complete captured recursive rawText/rawTask graphs and other captured fields match. This establishes neither wire-byte equality nor native internal-cache/function-registry equality. Both client journals were unopened. Removal, ending, archive and rejoin assertions remain UNRUN; this is not evidence of removed-row resurrection.

## Independent native mechanism proof

Full installed types are retained in `../promotion-0400/render76a5-native-*.stdout`. `TextProcessingContext.CallFunction` lines 308-336 evaluates parameters and returns the first parameter when no function body exists. `LocalizedTextManager.LoadLanguage` lines 231-270 resets then registers the XML functions. Actual Native `std_functions.xml:12` defines PLURAL using the argument's Plural field, falling back to a suffix. The requested item's raw name contains the explicit `loads of wrought iron` Plural field.

The isolated managed reproduction `../promotion-0400/render76a5-function-sensitivity.ps1` reconstructs the entire captured rawText graph without normalization. Loading actual local client TaleWorlds.Localization.dll SHA256 d75a5c89c26a09e587fab55c182b2962bb36708b4766a922bdf1f46a28212628, no registered functions produces the retained server text exactly. Registering only PLURAL from the actual native XML produces the retained owner text exactly. Both assertions passed in PowerShell 5.1. No C# compilation, game process, kit change or shared-helper change occurred. The first diagnostic invocation passed those assertions but failed while writing hashes; its failure receipt remains retained. The corrected invocation changes only the diagnostic hash helper and writes `render76a5-function-sensitivity-proof.json`.

This proves a sufficient native rendering mechanism. It does not observe the historical server registry or establish which startup path produced that state.

## Retained initialization evidence and limits

Client PID 14720 and observer PID 6512 native logs both explicitly contain `opening .../Native/ModuleData/Languages/std_functions.xml`, followed by `Loading localized text xml.` at 06:57:25.611/661 local time. Parent PowerShell line numbering is 636/638; rg numbering is 430/431 because of doubled CR newlines. Server PID 30456 has no matching line. All roles do have language_data index-loading messages. Absence of a server log line alone is not proof that registration never occurred.

The operator retained four exact d3af8bf44e11a9fdf0ce53bdfc2eada146ed7540 dedicated-server source files, staging-script provenance, role/native CampaignSystem receipts, and startup logs. Those four files contain no explicit language-initialization call; indirect/native calls remain possible. No per-role live Localization.dll hash, registry contents, active language, processor type, or per-resource file-content hash was captured. All-null assembly-hash comparisons were corrected to UNKNOWN and the initial invalid offline audit was preserved separately.

The selected run-windows.ps1 readiness-receipt hash c9a69852a1f25619a1f1628ff11526b7945729ded2f2d9f7178ecb1af0b55a06 differs from the exact pinned git blob hash fae33feb339d0954b56e58bcdf2f67dc3edab4d31420801f6d1ccf09f23c1c6a and from its computed CRLF variant 6fc4eef4f1fe5b37a7bc0adad27595eb7bfece1abd874ea72b16fb625d112fc3. The selected script bytes were not in the terminal package. This is an unresolved provenance difference, not evidence of a causal script fault. The single bounded exact-path read failed with UnauthorizedAccessException before hashing. No bytes or copy were retained, and before/read/after hashes are unavailable. No exact retained workspace copy path was known; no alternatives were guessed or shares enumerated. Recovery receipt: `crafting-dialogue-ui-operator/historical-run-script-recovery-attempt.json`. This audit is complete; historical selected-script recovery remains unavailable.

Local decompiled Module.Initialize loads localization indexes, while loading the active language is a separate operation in MBTextManager.ChangeLanguage/LocalizedTextManager.LoadLanguage. That distinction explains why index messages alone cannot settle initialization.

## Disposition and next decisive proof

Keep the automatic result and frozen packages unchanged. Do not charge a third SCRIPT failure, claim a removal PRODUCT regression, change the strict comparer, normalize text, or rerun this unchanged package.

The remaining causal boundary is dedicated-server localization initialization. Before adopting a correction, bind the actual dedicated Localization/Library binaries and native language XML inputs, inspect the exact server boot initialization, and obtain a source-bound regression showing native PLURAL availability after boot. Registering PLURAL only in the fixture could mask a production initialization discrepancy and is not an accepted correction. Any future in-game read-only receipt must expose active language, processor, function presence and local native render separately from captured raw journal metadata. Any production initialization correction belongs with its actual owner and requires review and validation before shared installation.

Cleanup is complete: owned processes/session absent, saves and original three staging files restored, source clean/pushed, prior freezes unchanged. Health growth owner118 MB / observer22 MB, zero health warnings. No new runtime or product/shared edit was performed during this diagnosis.
