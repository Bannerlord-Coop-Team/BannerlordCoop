# DefaultNotableSpawnModel

Installed type: `TaleWorlds.CampaignSystem.GameComponents.DefaultNotableSpawnModel`.

Normal support: unverified.
[Index](../source-paths.md) · [Mapping contract](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

This is structural navigation. Local returns, assignments and calls can belong to nested
or exclusive branches; resolve the complete caller/guard context before using any as an outcome.

| Path | Parent | Predicate / entry | Local outcome | Assignment targets | Call targets | Source span |
| --- | --- | --- | --- | --- | --- | --- |
| DefaultNotableSpawnModel.GetTargetNotableCountForSettlement.8.member | heroes.006 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return result; | Locally assigned targets: result, Occupation.Merchant, Occupation.GangLeader, Occupation.Artisan, _ | Calls in this member: none located | TaleWorlds.CampaignSystem.GameComponents.DefaultNotableSpawnModel@8-34 |
| DefaultNotableSpawnModel.GetTargetNotableCountForSettlement.8.guard-11 | heroes.006 | Source predicate is true: settlement.IsTown | Local true-branch returns: continues; final outcome requires surrounding control flow | True-branch assignment targets: result, Occupation.Merchant, Occupation.GangLeader, Occupation.Artisan, _ | True-branch calls: none located | TaleWorlds.CampaignSystem.GameComponents.DefaultNotableSpawnModel@8-34 |
| DefaultNotableSpawnModel.GetTargetNotableCountForSettlement.8.guard-21 | heroes.006 | Source predicate is true: settlement.IsVillage | Local true-branch returns: continues; final outcome requires surrounding control flow | True-branch assignment targets: result | True-branch calls: none located | TaleWorlds.CampaignSystem.GameComponents.DefaultNotableSpawnModel@8-34 |
| DefaultNotableSpawnModel.GetTargetNotableCountForSettlement.8.switch-25 | heroes.006 | This labeled arm is selected by the surrounding switch: case Occupation.Headman: | Interpret statements through the arm terminator; fallthrough and selector are not inferred | Inspect the labeled arm within the complete containing member | Resolve exactly the selected arm's calls and indirect effects | TaleWorlds.CampaignSystem.GameComponents.DefaultNotableSpawnModel@8-34 |
| DefaultNotableSpawnModel.GetTargetNotableCountForSettlement.8.switch-28 | heroes.006 | This labeled arm is selected by the surrounding switch: case Occupation.RuralNotable: | Interpret statements through the arm terminator; fallthrough and selector are not inferred | Inspect the labeled arm within the complete containing member | Resolve exactly the selected arm's calls and indirect effects | TaleWorlds.CampaignSystem.GameComponents.DefaultNotableSpawnModel@8-34 |
