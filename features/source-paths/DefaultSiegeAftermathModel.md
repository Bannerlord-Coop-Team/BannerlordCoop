# DefaultSiegeAftermathModel

Installed type: `TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeAftermathModel`.

Normal support: unverified.
[Index](../source-paths.md) · [Mapping contract](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

This is structural navigation. Local returns, assignments and calls can belong to nested
or exclusive branches; resolve the complete caller/guard context before using any as an outcome.

| Path | Parent | Predicate / entry | Local outcome | Assignment targets | Call targets | Source span |
| --- | --- | --- | --- | --- | --- | --- |
| DefaultSiegeAftermathModel.GetSiegeAftermathTraitXpChangeForPlayer.10.member | sieges.016 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return result; | Locally assigned targets: result | Calls in this member: none located | TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeAftermathModel@10-26 |
| DefaultSiegeAftermathModel.GetSiegeAftermathTraitXpChangeForPlayer.10.guard-13 | sieges.016 | Source predicate is true: trait == DefaultTraits.Mercy | Local true-branch returns: continues; final outcome requires surrounding control flow | True-branch assignment targets: result | True-branch calls: none located | TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeAftermathModel@10-26 |
| DefaultSiegeAftermathModel.GetSiegeAftermathTraitXpChangeForPlayer.10.switch-17 | sieges.016 | This labeled arm is selected by the surrounding switch: case SiegeAftermathAction.SiegeAftermath.Devastate: | Interpret statements through the arm terminator; fallthrough and selector are not inferred | Inspect the labeled arm within the complete containing member | Resolve exactly the selected arm's calls and indirect effects | TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeAftermathModel@10-26 |
| DefaultSiegeAftermathModel.GetSiegeAftermathTraitXpChangeForPlayer.10.switch-20 | sieges.016 | This labeled arm is selected by the surrounding switch: case SiegeAftermathAction.SiegeAftermath.ShowMercy: | Interpret statements through the arm terminator; fallthrough and selector are not inferred | Inspect the labeled arm within the complete containing member | Resolve exactly the selected arm's calls and indirect effects | TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeAftermathModel@10-26 |
